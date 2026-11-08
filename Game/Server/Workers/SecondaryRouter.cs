using System.Text.Json;

namespace GameServer.Workers;

using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;

public sealed class SecondaryRouter : ISecondaryRouter
{
    private static readonly TimeSpan EvictionDuration = TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, IGameRecipient> _recipientsById = new();
    private readonly Dictionary<string, DateTimeOffset> _evictedSpammersUntil = new();
    private readonly IGameScheduler _scheduler;
    private readonly IServerCommandDeserializer _commandDeserializer;
    private readonly IGameFactory _gameFactory;
    private readonly GameWorkerSettings _settings;
    private readonly GameWorkerSupportServices _supportServices;

    public SecondaryRouter(
        IGameScheduler scheduler,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        GameWorkerSettings settings,
        GameWorkerSupportServices supportServices)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(commandDeserializer);
        ArgumentNullException.ThrowIfNull(gameFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(supportServices);

        _scheduler = scheduler;
        _commandDeserializer = commandDeserializer;
        _gameFactory = gameFactory;
        _settings = settings;
        _supportServices = supportServices;
    }

    public IReadOnlyCollection<IGameRecipient> Recipients => _recipientsById.Values;

    public IGameRecipient? FindRecipient(string gameId)
    {
        return _recipientsById.GetValueOrDefault(gameId);
    }

    public void RegisterRecipient(string gameId, IGameRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _recipientsById[gameId] = recipient;
    }

    public void RemoveRecipient(string gameId)
    {
        _recipientsById.Remove(gameId);
    }

    public bool RouteMessage(
        InboundMessage message,
        bool isWorkerSealedByTick,
        IReadOnlyDictionary<string, int> migratingTargetByGameId)
    {
        ArgumentNullException.ThrowIfNull(message);

        return !isWorkerSealedByTick && CheckCooldownAndRoute(message, migratingTargetByGameId);
    }

    private bool CheckCooldownAndRoute(InboundMessage message, IReadOnlyDictionary<string, int> migratingTargetByGameId)
    {
        var hasCooldown = _evictedSpammersUntil.TryGetValue(message.GameId, out var cooldownUntil);
        var isCoolingDown = hasCooldown && DateTimeOffset.UtcNow < cooldownUntil;
        var isCooldownExpired = hasCooldown && !isCoolingDown;

        _ = isCooldownExpired && _evictedSpammersUntil.Remove(message.GameId);

        return !isCoolingDown && DispatchToRecipient(message, migratingTargetByGameId);
    }

    private bool DispatchToRecipient(InboundMessage message, IReadOnlyDictionary<string, int> migratingTargetByGameId)
    {
        var recipient = _recipientsById.GetValueOrDefault(message.GameId);
        var proxy = recipient as GameProxy;

        return proxy != null
            ? proxy.Forward(message)
            : RouteToTargetGame(message, recipient, migratingTargetByGameId);
    }

    private bool RouteToTargetGame(InboundMessage message, IGameRecipient? recipient, IReadOnlyDictionary<string, int> migratingTargetByGameId)
    {
        var targetGame = recipient as Game ?? CreateGameWithinLimit(message.GameId);

        var isSpammer = targetGame != null && targetGame.CommandQueue.Count >= _settings.MaxQueueLengthPerGame;
        var isSealed = targetGame != null && !isSpammer && targetGame.CommandQueue.IsSealedByTickMarker;
        var isNormal = targetGame != null && !isSpammer && !isSealed;

        return targetGame != null && (
            (isSpammer && EvictSpammer(targetGame)) ||
            (isSealed && ForwardSealedMessage(message, migratingTargetByGameId)) ||
            (isNormal && DeserializeAndEnqueue(message, targetGame))
        );
    }

    private bool EvictSpammer(Game targetGame)
    {
        _scheduler.Unregister(targetGame.GameId, GameTerminationReason.FloodQueueOverflow);
        _recipientsById.Remove(targetGame.GameId);
        _evictedSpammersUntil[targetGame.GameId] = DateTimeOffset.UtcNow.Add(EvictionDuration);
        return false;
    }

    private bool ForwardSealedMessage(InboundMessage message, IReadOnlyDictionary<string, int> migratingTargetByGameId)
    {
        var hasTarget = migratingTargetByGameId.TryGetValue(message.GameId, out var targetThreadId);
        _ = hasTarget && WriteOrOverflow(message, targetThreadId);
        return hasTarget;
    }

    private bool WriteOrOverflow(InboundMessage message, int targetThreadId)
    {
        var targetWriter = _supportServices.TransitResolver.ResolveWriter(targetThreadId);
        var written = targetWriter.TryWrite(message);
        _ = !written && OverflowTransit(message, targetWriter);
        return true;
    }

    private bool OverflowTransit(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter)
    {
        _supportServices.OverflowSink.Overflow(message, targetWriter);
        return true;
    }

    private bool DeserializeAndEnqueue(InboundMessage message, Game targetGame)
    {
        try
        {
            var command = _commandDeserializer.Deserialize(message, targetGame);
            targetGame.CommandQueue.Enqueue(command);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }

        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    public Game? CreateGameWithinLimit(string gameId)
    {
        return _scheduler.RegisteredGameCount < _settings.MaxActiveGames
            ? CreateAndRegisterGame(gameId)
            : null;
    }

    private Game CreateAndRegisterGame(string gameId)
    {
        var createdGame = _gameFactory.Create(gameId);
        _scheduler.Register(createdGame);
        _recipientsById[gameId] = createdGame;
        return createdGame;
    }
}
