using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace GameServer.Workers;

using Game;
using Game.Commands;
using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Resharding;
using Game = GameServer.Games.Game;

public sealed record WorkerCycleResult(
    int RoutedMessageCount,
    int RejectedMessageCount,
    int DispatchedCommandCount,
    int FailedCommandCount,
    IReadOnlyList<string> TerminatedGameIds);

public sealed class GameWorker
{
    private readonly IMessageQueueReader<InboundMessage> _inputReader;
    private readonly IGameScheduler _scheduler;
    private readonly IServerCommandDeserializer _commandDeserializer;
    private readonly IGameFactory _gameFactory;
    private readonly GameWorkerSettings _settings;
    private readonly GameWorkerSupportServices _supportServices;
    private readonly Dictionary<string, IGameRecipient> _recipientsById = new();
    private readonly Dictionary<string, int> _migratingTargetByGameId = new();
    private readonly FrozenDictionary<string, Func<InboundMessage, bool>> _controlHandlers;
    private bool _isSealedByTickMarker;
    private bool _isAwaitingFinalSnapshot;

    public GameWorker(
        IMessageQueueReader<InboundMessage> inputReader,
        IGameScheduler scheduler,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        GameWorkerSettings settings,
        GameWorkerSupportServices supportServices)
    {
        _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _commandDeserializer = commandDeserializer ?? throw new ArgumentNullException(nameof(commandDeserializer));
        _gameFactory = gameFactory ?? throw new ArgumentNullException(nameof(gameFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _supportServices = supportServices ?? throw new ArgumentNullException(nameof(supportServices));

        _controlHandlers = new Dictionary<string, Func<InboundMessage, bool>>
        {
            [ServerCommandNames.Resume] = HandleResumeMessage,
            [ServerCommandNames.TickMarker] = HandleTickMarkerMessage,
            [ServerCommandNames.MigrateOut] = HandleMigrateOutMessage,
            [ServerCommandNames.MigrateIn] = HandleMigrateInMessage
        }.ToFrozenDictionary();
    }

    public bool IsShutdownCompleted { get; private set; }

    public WorkerCycleResult ExecuteCycle(DateTimeOffset currentTime)
    {
        var cycleResult = RouteAndDispatch(_inputReader.DrainAllAvailable(), currentTime);

        FinalizeCompletedMigrations();
        FinalizeShutdownSnapshotIfDrained();

        return cycleResult;
    }

    public bool WaitForIncomingMessage(TimeSpan waitTimeout, CancellationToken cancellationToken)
    {
        return _inputReader.WaitForMessage(waitTimeout, cancellationToken);
    }

    private WorkerCycleResult RouteAndDispatch(IReadOnlyList<InboundMessage> incomingMessages, DateTimeOffset currentTime)
    {
        var routingResults = incomingMessages.Select(RouteOneMessage).ToList();
        var scheduleOutcome = _scheduler.ScheduleCycle(currentTime);

        return new WorkerCycleResult(
            routingResults.Count(accepted => accepted),
            routingResults.Count(accepted => !accepted),
            scheduleOutcome.DispatchedCommandCount,
            scheduleOutcome.FailedCommandCount,
            scheduleOutcome.TerminatedGameIds);
    }

    private bool RouteOneMessage(InboundMessage message)
    {
        var controlHandler = _controlHandlers.GetValueOrDefault(message.CommandName);

        if (controlHandler is not null)
        {
            return controlHandler(message);
        }

        if (_isSealedByTickMarker)
        {
            return false;
        }

        var recipient = _recipientsById.GetValueOrDefault(message.GameId);

        if (recipient is GameProxy gameProxy)
        {
            return gameProxy.Forward(message);
        }

        var targetGame = recipient as Game ?? CreateGameWithinLimit(message.GameId);

        if (targetGame is null)
        {
            return false;
        }

        if (targetGame.CommandQueue.Count >= _settings.MaxQueueLengthPerGame)
        {
            UnregisterGame(targetGame.GameId, GameTerminationReason.FloodQueueOverflow);
            return false;
        }

        if (targetGame.CommandQueue.IsSealedByTickMarker)
        {
            return false;
        }

        try
        {
            var command = _commandDeserializer.Deserialize(message, targetGame);
            targetGame.CommandQueue.Enqueue(command);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    private bool HandleResumeMessage(InboundMessage message)
    {
        var targetGame = _scheduler.FindGame(message.GameId);

        if (targetGame is null)
        {
            return false;
        }

        if (targetGame.IsAwaitingExternalEvent)
        {
            targetGame.ResumeFromExternalEvent();
        }

        targetGame.CommandQueue.Enqueue(new EmptyCommand());
        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    private bool HandleTickMarkerMessage(InboundMessage message)
    {
        using var argumentsDocument = JsonDocument.Parse(message.CommandArguments);
        var tickMarkerCommand = new EmptyCommand();

        if (argumentsDocument.RootElement.TryGetProperty("gameId", out var gameIdElement))
        {
            var targetGame = _scheduler.FindGame(gameIdElement.GetString()
                ?? throw new JsonException("Tick marker gameId must be a string."));

            if (targetGame is not null)
            {
                targetGame.CommandQueue.EnqueueTickMarker(tickMarkerCommand);
                _scheduler.NotifyCommandsQueued(targetGame);
            }

            return true;
        }

        _isSealedByTickMarker = true;
        _isAwaitingFinalSnapshot = true;
        _recipientsById.Values
            .OfType<Game>()
            .ToList()
            .ForEach(game =>
            {
                game.CommandQueue.EnqueueTickMarker(tickMarkerCommand);
                _scheduler.NotifyCommandsQueued(game);
            });

        return true;
    }

    private bool HandleMigrateOutMessage(InboundMessage message)
    {
        using var argumentsDocument = JsonDocument.Parse(message.CommandArguments);
        var targetThreadId = argumentsDocument.RootElement.GetProperty("targetThreadId").GetInt32();
        var migratingGame = _scheduler.FindGame(message.GameId);

        if (migratingGame is null || _migratingTargetByGameId.ContainsKey(message.GameId))
        {
            return false;
        }

        migratingGame.CommandQueue.EnqueueTickMarker(new EmptyCommand());
        _scheduler.NotifyCommandsQueued(migratingGame);
        _migratingTargetByGameId[message.GameId] = targetThreadId;
        return true;
    }

    private bool HandleMigrateInMessage(InboundMessage message)
    {
        try
        {
            var snapshot = _supportServices.SnapshotCodec.Deserialize(message.CommandArguments);

            if (_recipientsById.ContainsKey(snapshot.GameId))
            {
                return false;
            }

            var restoredGame = RestoreGame(snapshot);

            _scheduler.Register(restoredGame);
            _recipientsById[snapshot.GameId] = restoredGame;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private void FinalizeCompletedMigrations()
    {
        var completedGameIds = _migratingTargetByGameId.Keys.ToList();

        completedGameIds.ForEach(gameId =>
        {
            var migratedGame = _scheduler.FindGame(gameId);

            if (migratedGame is null
                || !migratedGame.CommandQueue.IsSealedByTickMarker
                || migratedGame.CommandQueue.HasCommands)
            {
                return;
            }

            try
            {
                TransferGameToThread(gameId, migratedGame, _migratingTargetByGameId[gameId]);
                _migratingTargetByGameId.Remove(gameId);
            }
            catch (KeyNotFoundException)
            {
                return;
            }
        });
    }

    private void TransferGameToThread(string gameId, Game migratedGame, int targetThreadId)
    {
        var snapshotPayload = _supportServices.SnapshotCodec.Serialize(CaptureSnapshot(migratedGame));
        var migrateInMessage = new InboundMessage(
            targetThreadId,
            gameId,
            "system",
            ServerCommandNames.MigrateIn,
            snapshotPayload);

        UnregisterGame(gameId, GameTerminationReason.GameTransferred);

        var targetWriter = _supportServices.TransitResolver.ResolveWriter(targetThreadId);
        _recipientsById[gameId] = new GameProxy(gameId, targetWriter, _supportServices.OverflowSink);

        if (!targetWriter.TryWrite(migrateInMessage))
        {
            _supportServices.OverflowSink.Overflow(migrateInMessage, targetWriter);
        }
    }

    private void FinalizeShutdownSnapshotIfDrained()
    {
        if (!_isAwaitingFinalSnapshot || HasUnfinishedGameWork())
        {
            return;
        }

        _recipientsById.Values
            .OfType<Game>()
            .ToList()
            .ForEach(game => _supportServices.SnapshotStore.Save(CaptureSnapshot(game)));

        _isAwaitingFinalSnapshot = false;
        IsShutdownCompleted = true;
    }

    private bool HasUnfinishedGameWork()
    {
        return _migratingTargetByGameId.Count > 0
            || _scheduler.TotalQueuedCommandCount > 0
            || _recipientsById.Values
                .OfType<Game>()
                .Any(game => !game.CommandQueue.IsSealedByTickMarker);
    }

    private GameSnapshot CaptureSnapshot(Game game)
    {
        var itemSnapshots = game.Scope.ItemIds
            .Select(itemId => new GameObjectSnapshot(itemId, SerializeItemProperties(game.Scope.GetItem(itemId))))
            .ToList();

        return new GameSnapshot(game.GameId, game.Priority, itemSnapshots);
    }

    private static string SerializeItemProperties(IGameItem gameItem)
    {
        using var propertiesStream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(propertiesStream))
        {
            writer.WriteStartObject();

            gameItem.PropertyNames.ToList().ForEach(propertyName =>
            {
                writer.WritePropertyName(propertyName);
                WritePropertyValue(writer, gameItem.GetProperty(propertyName));
            });

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(propertiesStream.ToArray());
    }

    private static void WritePropertyValue(Utf8JsonWriter writer, object propertyValue)
    {
        switch (propertyValue)
        {
            case Vector vector:
                writer.WriteStartArray();
                vector.Coordinates.ToList().ForEach(coordinate => writer.WriteNumberValue(coordinate));
                writer.WriteEndArray();
                break;
            case string textValue:
                writer.WriteStringValue(textValue);
                break;
            case int numberValue:
                writer.WriteNumberValue(numberValue);
                break;
            default:
                writer.WriteStringValue(propertyValue.ToString());
                break;
        }
    }

    private Game RestoreGame(GameSnapshot snapshot)
    {
        var restoredScope = new Scope();

        snapshot.Items.ToList().ForEach(itemSnapshot =>
            restoredScope.Register(itemSnapshot.ItemId, RestoreItem(itemSnapshot)));

        return new Game(snapshot.GameId, snapshot.Priority, restoredScope);
    }

    private static GameItem RestoreItem(GameObjectSnapshot itemSnapshot)
    {
        var restoredItem = new GameItem();

        using var propertiesDocument = JsonDocument.Parse(itemSnapshot.SerializedProperties);

        propertiesDocument.RootElement
            .EnumerateObject()
            .ToList()
            .ForEach(property => restoredItem.SetProperty(property.Name, RestorePropertyValue(property.Value)));

        return restoredItem;
    }

    private static object RestorePropertyValue(JsonElement propertyValue)
    {
        return propertyValue.ValueKind switch
        {
            JsonValueKind.Array => new Vector(propertyValue.EnumerateArray().Select(component => component.GetInt32()).ToArray()),
            JsonValueKind.String => propertyValue.GetString()
                ?? throw new ArgumentException("Snapshot string property must not be null."),
            JsonValueKind.Number => propertyValue.GetInt32(),
            _ => throw new ArgumentException($"Snapshot property kind '{propertyValue.ValueKind}' is not supported.")
        };
    }

    private void UnregisterGame(string gameId, GameTerminationReason terminationReason)
    {
        _scheduler.Unregister(gameId, terminationReason);
        _recipientsById.Remove(gameId);
    }

    private Game? CreateGameWithinLimit(string gameId)
    {
        if (_scheduler.RegisteredGameCount >= _settings.MaxActiveGames)
        {
            return null;
        }

        var createdGame = _gameFactory.Create(gameId);
        _scheduler.Register(createdGame);
        _recipientsById[gameId] = createdGame;

        return createdGame;
    }
}
