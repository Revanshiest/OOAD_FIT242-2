namespace GameServer.Workers;

using System.Collections.Frozen;
using Game.Commands;
using Game.IoC;
using GameServer.Games;
using GameServer.Messaging;
using Game = GameServer.Games.Game;

public sealed class PriorityRoundRobinScheduler : IGameScheduler
{
    private readonly Dictionary<string, Game> _gamesById = new();
    private readonly HashSet<string> _readyGameIds = new();
    private readonly Queue<Game> _readyHighPriorityGames = new();
    private readonly Queue<Game> _readyNormalPriorityGames = new();
    private readonly FrozenDictionary<GamePriority, Queue<Game>> _readyRingsByPriority;
    private readonly GameWorkerSettings _settings;
    private readonly IGameRateLimiter _rateLimiter;
    private readonly IGameTerminationObserver _terminationObserver;

    public event Action<ICommand, Exception>? CommandExecutionFailed;

    public PriorityRoundRobinScheduler(
        GameWorkerSettings settings,
        IGameRateLimiter rateLimiter,
        IGameTerminationObserver terminationObserver)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(terminationObserver);

        _settings = settings;
        _rateLimiter = rateLimiter;
        _terminationObserver = terminationObserver;
        _readyRingsByPriority = new Dictionary<GamePriority, Queue<Game>>
        {
            [GamePriority.High] = _readyHighPriorityGames,
            [GamePriority.Normal] = _readyNormalPriorityGames
        }.ToFrozenDictionary();
    }

    public int RegisteredGameCount => _gamesById.Count;

    public int ReadyGameCount => _readyGameIds.Count;

    public int TotalQueuedCommandCount => _gamesById.Values.Sum(game => game.CommandQueue.Count);

    public bool HasReadyCommands => ReadyGameCount > 0;

    public IReadOnlyCollection<string> RegisteredGameIds => _gamesById.Keys;

    public void Register(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _ = _gamesById.TryAdd(game.GameId, game)
            ? 0
            : throw new InvalidOperationException($"Game '{game.GameId}' is already registered in the scheduler.");

        _ = game.CommandQueue.HasCommands && !game.IsAwaitingExternalEvent && EnqueueReady(game);
    }

    public Game? FindGame(string gameId)
    {
        return _gamesById.GetValueOrDefault(gameId);
    }

    public void NotifyCommandsQueued(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _ = _gamesById.TryGetValue(game.GameId, out var currentGame)
            && ReferenceEquals(game, currentGame)
            && game.CommandQueue.HasCommands
            && !game.IsAwaitingExternalEvent
            && EnqueueReady(game);
    }

    public ScheduleOutcome ScheduleCycle(DateTimeOffset currentTime)
    {
        var terminatedGameIds = TerminateExpiredAwaitingGames(currentTime);

        var highSnapshot = DrainRingSnapshot(_readyHighPriorityGames);
        var normalSnapshot = DrainRingSnapshot(_readyNormalPriorityGames);

        int highPriorityIndex = 0;
        int normalPriorityIndex = 0;
        int dispatchedCommandCount = 0;
        int failedCommandCount = 0;

        const int HighToNormalRatio = 3;

        while (highPriorityIndex < highSnapshot.Length || normalPriorityIndex < normalSnapshot.Length)
        {
            int highPriorityBatchCount = 0;
            while (highPriorityIndex < highSnapshot.Length && (highPriorityBatchCount < HighToNormalRatio || normalPriorityIndex >= normalSnapshot.Length))
            {
                var (dispatched, failed) = ServeGame(highSnapshot[highPriorityIndex++], currentTime, _readyHighPriorityGames);
                dispatchedCommandCount += dispatched;
                failedCommandCount += failed;
                highPriorityBatchCount++;
            }

            while (normalPriorityIndex < normalSnapshot.Length)
            {
                var (dispatched, failed) = ServeGame(normalSnapshot[normalPriorityIndex++], currentTime, _readyNormalPriorityGames);
                dispatchedCommandCount += dispatched;
                failedCommandCount += failed;
                break;
            }
        }

        return new ScheduleOutcome(
            dispatchedCommandCount,
            failedCommandCount,
            terminatedGameIds);
    }

    private Game[] DrainRingSnapshot(Queue<Game> readyRing)
    {
        var ringSnapshot = readyRing.ToArray();
        readyRing.Clear();
        foreach (var game in ringSnapshot)
        {
            if (_gamesById.TryGetValue(game.GameId, out var currentGame) && ReferenceEquals(game, currentGame))
            {
                _readyGameIds.Remove(game.GameId);
            }
        }
        return ringSnapshot;
    }

    public void Unregister(string gameId, GameTerminationReason terminationReason)
    {
        _ = _gamesById.Remove(gameId)
            ? 0
            : throw new KeyNotFoundException($"Game '{gameId}' is not registered in the scheduler.");

        _readyGameIds.Remove(gameId);
        _terminationObserver.NotifyGameTerminated(gameId, terminationReason);
    }

    private IReadOnlyList<string> TerminateExpiredAwaitingGames(DateTimeOffset currentTime)
    {
        var expiredGameIds = _gamesById.Values
            .Where(game => game.IsAwaitingExternalEvent && game.AwaitDuration(currentTime) > _settings.MaxExternalAwaitDuration)
            .Select(game => game.GameId)
            .ToList();

        foreach (var gameId in expiredGameIds)
        {
            Unregister(gameId, GameTerminationReason.WatchdogAwaitTimeout);
        }

        return expiredGameIds;
    }

    private (int DispatchedCommandCount, int FailedCommandCount) ServeGame(Game game, DateTimeOffset currentTime, Queue<Game> readyRing)
    {
        var isKnown = _gamesById.TryGetValue(game.GameId, out var currentGame)
            && ReferenceEquals(game, currentGame)
            && !game.IsAwaitingExternalEvent;
        var admitted = isKnown && _rateLimiter.TryAdmit(game.GameId, currentTime);

        _ = isKnown && !admitted && EnqueueReadyInto(readyRing, game);

        return isKnown && admitted
            ? ExecuteGameBatch(game, readyRing)
            : (0, 0);
    }

    private (int DispatchedCommandCount, int FailedCommandCount) ExecuteGameBatch(Game game, Queue<Game> readyRing)
    {
        object? previousScope = null;
        var gameIocScope = game.Scope.IocScope;

        while (gameIocScope is not null)
        {
            try
            {
                previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
                Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameIocScope!).Execute();
            }
            catch
            {
            }
            break;
        }

        try
        {
            var (dispatched, failed) = game.ExecuteBatch(
                _settings.CommandQuantumPerGameActivation,
                (command, exception) => CommandExecutionFailed?.Invoke(command, exception));

            _ = game.CommandQueue.HasCommands && !game.IsAwaitingExternalEvent && EnqueueReadyInto(readyRing, game);

            return (dispatched, failed);
        }
        finally
        {
            while (previousScope is not null)
            {
                try
                {
                    Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope!).Execute();
                }
                catch
                {
                }
                break;
            }
        }
    }

    private bool EnqueueReady(Game game)
    {
        return EnqueueReadyInto(_readyRingsByPriority[game.Priority], game);
    }

    private bool EnqueueReadyInto(Queue<Game> readyRing, Game game)
    {
        var shouldEnqueue = _readyGameIds.Add(game.GameId);
        _ = shouldEnqueue && EnqueueDirect(readyRing, game);
        return shouldEnqueue;
    }

    private static bool EnqueueDirect(Queue<Game> readyRing, Game game)
    {
        readyRing.Enqueue(game);
        return true;
    }
}
