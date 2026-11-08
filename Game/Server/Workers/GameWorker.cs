using System.Collections.Concurrent;
using System.Text.Json;

namespace GameServer.Workers;

using Game.Commands;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using Game = GameServer.Games.Game;

public sealed record WorkerCycleResult(
    int RoutedMessageCount,
    int RejectedMessageCount,
    int DispatchedCommandCount,
    int FailedCommandCount,
    IReadOnlyList<string> TerminatedGameIds);

public sealed class GameWorker : IGameWorker
{
    private static readonly TimeSpan ProxyTtl = TimeSpan.FromSeconds(5);
    private readonly int _threadId;
    private readonly IMessageQueueReader<InboundMessage> _inputQueue;
    private readonly ISecondaryRouter _secondaryRouter;
    private readonly IGameScheduler _scheduler;
    private readonly IGameSnapshotService _snapshotService;
    private readonly GameWorkerSupportServices _supportServices;
    private readonly ConcurrentDictionary<string, Func<InboundMessage, bool>> _controlHandlers;
    private readonly ConcurrentDictionary<string, int> _migratingTargetByGameId = new();
    private volatile bool _isSealedByTickMarker;
    private volatile bool _isAwaitingFinalSnapshot;
    private volatile bool _isShutdownCompleted;

    public GameWorker(
        IMessageQueueReader<InboundMessage> inputReader,
        IGameScheduler scheduler,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        GameWorkerSettings settings,
        GameWorkerSupportServices supportServices)
        : this(
            0,
            inputReader,
            new SecondaryRouter(scheduler, commandDeserializer, gameFactory, settings, supportServices),
            scheduler,
            new GameSnapshotService(),
            supportServices,
            null)
    {
    }

    public GameWorker(
        int threadId,
        IMessageQueueReader<InboundMessage> inputQueue,
        ISecondaryRouter secondaryRouter,
        IGameScheduler scheduler,
        IGameSnapshotService snapshotService,
        GameWorkerSupportServices supportServices)
        : this(
            threadId,
            inputQueue,
            secondaryRouter,
            scheduler,
            snapshotService,
            supportServices,
            null)
    {
    }

    public GameWorker(
        int threadId,
        IMessageQueueReader<InboundMessage> inputQueue,
        ISecondaryRouter secondaryRouter,
        IGameScheduler scheduler,
        IGameSnapshotService snapshotService,
        GameWorkerSupportServices supportServices,
        IReadOnlyDictionary<string, Func<InboundMessage, bool>>? customControlHandlers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threadId, nameof(threadId));
        ArgumentNullException.ThrowIfNull(inputQueue);
        ArgumentNullException.ThrowIfNull(secondaryRouter);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentNullException.ThrowIfNull(supportServices);

        _threadId = threadId;
        _inputQueue = inputQueue;
        _secondaryRouter = secondaryRouter;
        _scheduler = scheduler;
        _snapshotService = snapshotService;
        _supportServices = supportServices;

        _controlHandlers = new ConcurrentDictionary<string, Func<InboundMessage, bool>>
        {
            [ServerCommandNames.Resume] = HandleResumeMessage,
            [ServerCommandNames.TickMarker] = HandleTickMarkerMessage,
            [ServerCommandNames.MigrateOut] = HandleMigrateOutMessage,
            [ServerCommandNames.MigrateIn] = HandleMigrateInMessage
        };

        foreach (var customControlHandler in customControlHandlers ?? Enumerable.Empty<KeyValuePair<string, Func<InboundMessage, bool>>>())
        {
            _controlHandlers[customControlHandler.Key] = customControlHandler.Value;
        }
    }

    public void RegisterControlHandler(string commandName, Func<InboundMessage, bool> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(handler);

        _controlHandlers[commandName] = handler;
    }

    public int ThreadId => _threadId;

    public ISecondaryRouter SecondaryRouter => _secondaryRouter;

    public bool IsSealedByTickMarker => _isSealedByTickMarker;

    public bool IsShutdownCompleted
    {
        get => _isShutdownCompleted;
        private set => _isShutdownCompleted = value;
    }

    public bool HasReadyWork => _scheduler.HasReadyCommands;

    public WorkerCycleResult ExecuteCycle(DateTimeOffset currentTime)
    {
        var incomingMessages = _inputQueue.DrainAllAvailable();

        var cycleResult = RouteAndDispatch(incomingMessages, currentTime);

        FinalizeCompletedMigrations();
        PruneExpiredProxies(currentTime);
        FinalizeShutdownSnapshotIfDrained();

        return cycleResult;
    }

    public WorkerCycleResult RouteAndDispatch(IReadOnlyList<InboundMessage> incomingMessages, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(incomingMessages);

        int routedCount = incomingMessages.Count(RouteOneMessage);
        int rejectedCount = incomingMessages.Count - routedCount;

        var scheduleOutcome = _scheduler.ScheduleCycle(currentTime);

        return new WorkerCycleResult(
            routedCount,
            rejectedCount,
            scheduleOutcome.DispatchedCommandCount,
            scheduleOutcome.FailedCommandCount,
            scheduleOutcome.TerminatedGameIds);
    }

    private bool RouteOneMessage(InboundMessage message)
    {
        var handler = _controlHandlers.GetValueOrDefault(message.CommandName)
            ?? TryResolveDynamicControlHandler(message.CommandName);

        return handler != null
            ? ExecuteControlHandlerSafely(handler, message)
            : _secondaryRouter.RouteMessage(message, _isSealedByTickMarker, _migratingTargetByGameId);
    }

    private static Func<InboundMessage, bool>? TryResolveDynamicControlHandler(string commandName)
    {
        try
        {
            return Ioc.Resolve<Func<InboundMessage, bool>>($"Server.ControlHandler.{commandName}");
        }
        catch
        {
            return null;
        }
    }

    private static bool ExecuteControlHandlerSafely(Func<InboundMessage, bool> handler, InboundMessage message)
    {
        try
        {
            return handler(message);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool HandleResumeMessage(InboundMessage message)
    {
        var targetGame = _scheduler.FindGame(message.GameId);
        return targetGame != null && ResumeGame(targetGame);
    }

    private bool ResumeGame(Game targetGame)
    {
        _ = targetGame.IsAwaitingExternalEvent && ResumeTargetGame(targetGame);
        targetGame.CommandQueue.Enqueue(new EmptyCommand());
        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    private static bool ResumeTargetGame(Game targetGame)
    {
        targetGame.ResumeFromExternalEvent();
        return true;
    }

    private bool HandleTickMarkerMessage(InboundMessage message)
    {
        try
        {
            using var argumentsDocument = JsonDocument.Parse(message.CommandArguments);
            var hasGameId = argumentsDocument.RootElement.TryGetProperty("gameId", out var gameIdElement);

            return hasGameId
                ? HandleIndividualTickMarker(gameIdElement)
                : HandleGlobalTickMarker();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool HandleIndividualTickMarker(JsonElement gameIdElement)
    {
        var targetGame = _scheduler.FindGame(gameIdElement.GetString()
            ?? throw new JsonException("Tick marker gameId must be a string."));

        _ = targetGame != null && EnqueueTickMarkerAndNotify(targetGame);
        return true;
    }

    private bool EnqueueTickMarkerAndNotify(Game targetGame)
    {
        targetGame.CommandQueue.EnqueueTickMarker(new EmptyCommand());
        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    private bool HandleGlobalTickMarker()
    {
        _isSealedByTickMarker = true;
        _isAwaitingFinalSnapshot = true;

        foreach (var game in _secondaryRouter.Recipients.OfType<Game>())
        {
            game.CommandQueue.EnqueueTickMarker(new EmptyCommand());
            _scheduler.NotifyCommandsQueued(game);
        }

        return true;
    }

    private bool HandleMigrateOutMessage(InboundMessage message)
    {
        try
        {
            using var argumentsDocument = JsonDocument.Parse(message.CommandArguments);
            var targetThreadId = argumentsDocument.RootElement.GetProperty("targetThreadId").GetInt32();
            var migratingGame = _scheduler.FindGame(message.GameId);
            var canMigrate = migratingGame is not null && !_migratingTargetByGameId.ContainsKey(message.GameId);

            return canMigrate && ExecuteMigrateOut(message.GameId, migratingGame!, targetThreadId);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private bool ExecuteMigrateOut(string gameId, Game migratingGame, int targetThreadId)
    {
        migratingGame.CommandQueue.EnqueueTickMarker(new EmptyCommand());
        _scheduler.NotifyCommandsQueued(migratingGame);
        _migratingTargetByGameId[gameId] = targetThreadId;
        return true;
    }

    private bool HandleMigrateInMessage(InboundMessage message)
    {
        try
        {
            var snapshot = _supportServices.SnapshotCodec.Deserialize(message.CommandArguments);
            var existingRecipient = _secondaryRouter.FindRecipient(snapshot.GameId);

            return existingRecipient is not null
                ? MergeIntoExistingRecipient(existingRecipient, snapshot)
                : RestoreFreshMigratedGame(snapshot);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or KeyNotFoundException)
        {
            return false;
        }
    }

    private bool MergeIntoExistingRecipient(IGameRecipient existingRecipient, GameSnapshot snapshot)
    {
        var existingGame = existingRecipient as Game;
        return existingGame != null && ExecuteMerge(existingGame, snapshot);
    }

    private bool ExecuteMerge(Game existingGame, GameSnapshot snapshot)
    {
        foreach (var itemSnapshot in snapshot.Items)
        {
            _ = !existingGame.Scope.Contains(itemSnapshot.ItemId)
                && RegisterRestoredItem(existingGame.Scope, itemSnapshot);
        }

        foreach (var commandDescriptor in snapshot.QueuedCommands ?? [])
        {
            var command = GameSnapshotService.RestoreCommand(commandDescriptor, existingGame, existingGame.Scope);
            existingGame.CommandQueue.Enqueue(command);
        }

        _ = (snapshot.QueuedCommands?.Count ?? 0) > 0 && NotifyQueued(existingGame);
        return true;
    }

    private static bool RegisterRestoredItem(IScope scope, GameObjectSnapshot itemSnapshot)
    {
        scope.Register(itemSnapshot.ItemId, GameSnapshotService.RestoreItem(itemSnapshot));
        return true;
    }

    private bool NotifyQueued(Game game)
    {
        _scheduler.NotifyCommandsQueued(game);
        return true;
    }

    private bool RestoreFreshMigratedGame(GameSnapshot snapshot)
    {
        var restoredGame = _snapshotService.RestoreGame(snapshot);
        _secondaryRouter.RegisterRecipient(snapshot.GameId, restoredGame);
        _scheduler.Register(restoredGame);
        _scheduler.NotifyCommandsQueued(restoredGame);
        return true;
    }

    public bool WaitForIncomingMessage(TimeSpan waitTimeout, CancellationToken cancellationToken)
    {
        return _inputQueue.WaitForMessage(waitTimeout, cancellationToken);
    }

    public void MigrateOutGame(string gameId, int targetThreadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentOutOfRangeException.ThrowIfNegative(targetThreadId, nameof(targetThreadId));

        var migratingGame = _scheduler.FindGame(gameId)
            ?? throw new KeyNotFoundException($"Game '{gameId}' is not found on worker thread {_threadId}.");

        var snapshot = _snapshotService.CaptureSnapshot(migratingGame);
        var snapshotPayload = _supportServices.SnapshotCodec.Serialize(snapshot);
        var migrateInMessage = new InboundMessage(
            targetThreadId,
            gameId,
            "system",
            ServerCommandNames.MigrateIn,
            snapshotPayload);

        SendMigrateInMessage(migrateInMessage, targetThreadId, gameId);
    }

    private void SendMigrateInMessage(InboundMessage migrateInMessage, int targetThreadId, string gameId)
    {
        UnregisterGame(gameId, GameTerminationReason.GameTransferred);

        var targetWriter = _supportServices.TransitResolver.ResolveWriter(targetThreadId);
        _secondaryRouter.RegisterRecipient(gameId, new GameProxy(gameId, targetWriter, _supportServices.OverflowSink));

        var written = targetWriter.TryWrite(migrateInMessage);
        _ = !written && OverflowSink(migrateInMessage, targetWriter);
    }

    private void FinalizeCompletedMigrations()
    {
        var completedGameIds = _migratingTargetByGameId.Keys.ToList();
        foreach (var gameId in completedGameIds)
        {
            var hasTarget = _migratingTargetByGameId.TryGetValue(gameId, out var targetThreadId);
            var migratedGame = _scheduler.FindGame(gameId);
            var isReady = hasTarget
                && migratedGame is not null
                && migratedGame.CommandQueue.IsSealedByTickMarker
                && !migratedGame.CommandQueue.HasCommands;

            _ = isReady && TransferAndUnregisterMigrating(gameId, migratedGame!, targetThreadId);
        }
    }

    private bool TransferAndUnregisterMigrating(string gameId, Game migratedGame, int targetThreadId)
    {
        try
        {
            var snapshot = _snapshotService.CaptureSnapshot(migratedGame);
            var snapshotPayload = _supportServices.SnapshotCodec.Serialize(snapshot);
            var migrateInMessage = new InboundMessage(
                targetThreadId,
                gameId,
                "system",
                ServerCommandNames.MigrateIn,
                snapshotPayload);

            SendMigrateInMessage(migrateInMessage, targetThreadId, gameId);
            _migratingTargetByGameId.TryRemove(gameId, out _);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private bool OverflowSink(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter)
    {
        _supportServices.OverflowSink.Overflow(message, targetWriter);
        return true;
    }

    private void PruneExpiredProxies(DateTimeOffset currentTime)
    {
        var expiredProxies = _secondaryRouter.Recipients
            .OfType<GameProxy>()
            .Where(proxy => proxy.Age(currentTime) > ProxyTtl)
            .ToList();

        foreach (var proxy in expiredProxies)
        {
            _secondaryRouter.RemoveRecipient(proxy.GameId);
        }
    }

    private void FinalizeShutdownSnapshotIfDrained()
    {
        var shouldFinalize = _isAwaitingFinalSnapshot && !HasUnfinishedGameWork();
        while (shouldFinalize)
        {
            foreach (var game in _secondaryRouter.Recipients.OfType<Game>())
            {
                _supportServices.SnapshotStore.Save(_snapshotService.CaptureSnapshot(game));
            }

            _isAwaitingFinalSnapshot = false;
            IsShutdownCompleted = true;
            break;
        }
    }

    private bool HasUnfinishedGameWork()
    {
        return _migratingTargetByGameId.Count > 0
            || _secondaryRouter.Recipients
                .OfType<Game>()
                .Any(game => !game.IsAwaitingExternalEvent && (game.CommandQueue.HasCommands || !game.CommandQueue.IsSealedByTickMarker));
    }

    private void UnregisterGame(string gameId, GameTerminationReason terminationReason)
    {
        _scheduler.Unregister(gameId, terminationReason);
        _secondaryRouter.RemoveRecipient(gameId);
    }
}
