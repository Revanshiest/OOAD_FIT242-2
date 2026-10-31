namespace GameServer.Workers;

using GameServer.Games;
using Game = GameServer.Games.Game;

public sealed class PriorityRoundRobinScheduler : IGameScheduler
{
    private readonly GameWorkerSettings _settings;
    private readonly CommandRateLimiter _rateLimiter;
    private readonly IGameTerminationObserver _terminationObserver;
    private readonly Dictionary<string, Game> _gamesById = new();
    private readonly Queue<Game> _readyHighPriorityGames = new();
    private readonly Queue<Game> _readyNormalPriorityGames = new();
    private readonly HashSet<string> _readyGameIds = new();

    public PriorityRoundRobinScheduler(
        GameWorkerSettings settings,
        CommandRateLimiter rateLimiter,
        IGameTerminationObserver terminationObserver)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _terminationObserver = terminationObserver ?? throw new ArgumentNullException(nameof(terminationObserver));
    }

    public int RegisteredGameCount => _gamesById.Count;

    public void Register(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_gamesById.TryAdd(game.GameId, game))
        {
            throw new InvalidOperationException($"Game '{game.GameId}' is already registered in the scheduler.");
        }

        if (game.CommandQueue.HasCommands)
        {
            EnqueueReady(game);
        }
    }

    public Game? FindGame(string gameId)
    {
        return _gamesById.GetValueOrDefault(gameId);
    }

    public void NotifyCommandsQueued(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!game.IsAwaitingExternalEvent)
        {
            EnqueueReady(game);
        }
    }

    public ScheduleOutcome ScheduleCycle(DateTimeOffset currentTime)
    {
        var terminatedGameIds = TerminateExpiredAwaitingGames(currentTime);
        var highPriorityOutcome = DispatchFromRing(_readyHighPriorityGames, currentTime);
        var normalPriorityOutcome = DispatchFromRing(_readyNormalPriorityGames, currentTime);

        return new ScheduleOutcome(
            highPriorityOutcome.DispatchedCommandCount + normalPriorityOutcome.DispatchedCommandCount,
            highPriorityOutcome.FailedCommandCount + normalPriorityOutcome.FailedCommandCount,
            terminatedGameIds);
    }

    public void Unregister(string gameId, GameTerminationReason terminationReason)
    {
        if (!_gamesById.Remove(gameId))
        {
            throw new KeyNotFoundException($"Game '{gameId}' is not registered in the scheduler.");
        }

        _readyGameIds.Remove(gameId);
        _terminationObserver.NotifyGameTerminated(gameId, terminationReason);
    }

    private IReadOnlyList<string> TerminateExpiredAwaitingGames(DateTimeOffset currentTime)
    {
        var expiredGameIds = _gamesById.Values
            .Where(game => game.IsAwaitingExternalEvent && game.AwaitDuration(currentTime) > _settings.MaxExternalAwaitDuration)
            .Select(game => game.GameId)
            .ToList();

        expiredGameIds.ForEach(gameId => Unregister(gameId, GameTerminationReason.WatchdogAwaitTimeout));

        return expiredGameIds;
    }

    private (int DispatchedCommandCount, int FailedCommandCount) DispatchFromRing(Queue<Game> readyRing, DateTimeOffset currentTime)
    {
        var ringSnapshot = readyRing.ToArray();
        readyRing.Clear();
        Array.ForEach(ringSnapshot, game => _readyGameIds.Remove(game.GameId));

        var perGameOutcomes = ringSnapshot.Select(game => ServeGame(game, currentTime, readyRing)).ToList();

        return (
            perGameOutcomes.Sum(outcome => outcome.DispatchedCommandCount),
            perGameOutcomes.Sum(outcome => outcome.FailedCommandCount));
    }

    private (int DispatchedCommandCount, int FailedCommandCount) ServeGame(Game game, DateTimeOffset currentTime, Queue<Game> readyRing)
    {
        if (!_gamesById.ContainsKey(game.GameId))
        {
            return (0, 0);
        }

        if (!_rateLimiter.TryAdmit(game.GameId, currentTime))
        {
            EnqueueReadyInto(readyRing, game);
            return (0, 0);
        }

        var commandBatch = game.CommandQueue.DequeueBatch(_settings.CommandQuantumPerGameActivation);
        var executionResults = commandBatch.Select(ExecuteCommandSafely).ToList();

        if (game.CommandQueue.HasCommands && !game.IsAwaitingExternalEvent)
        {
            EnqueueReadyInto(readyRing, game);
        }

        return (executionResults.Count(executed => executed), executionResults.Count(executed => !executed));
    }

    private static bool ExecuteCommandSafely(ICommand command)
    {
        try
        {
            command.Execute();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void EnqueueReady(Game game)
    {
        EnqueueReadyInto(RingForPriority(game.Priority), game);
    }

    private void EnqueueReadyInto(Queue<Game> readyRing, Game game)
    {
        if (_readyGameIds.Add(game.GameId))
        {
            readyRing.Enqueue(game);
        }
    }

    private Queue<Game> RingForPriority(GamePriority priority)
    {
        return priority == GamePriority.High ? _readyHighPriorityGames : _readyNormalPriorityGames;
    }
}
