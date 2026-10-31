namespace Game.Tests.Server.Workers;

using GameServer.Games;
using GameServer.Workers;
using Game = GameServer.Games.Game;

public class PriorityRoundRobinSchedulerTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const int CommandQuantum = 4;

    private readonly List<string> _executionLog = [];
    private readonly RecordingGameTerminationObserver _terminationObserver = new();

    [Fact]
    public void Register_And_FindGame_ReturnRegisteredGame()
    {
        var scheduler = CreateScheduler();
        var game = CreateGame("game-1");

        scheduler.Register(game);

        Assert.Same(game, scheduler.FindGame("game-1"));
        Assert.Equal(1, scheduler.RegisteredGameCount);
    }

    [Fact]
    public void Register_ThrowsInvalidOperationException_WhenGameAlreadyRegistered()
    {
        var scheduler = CreateScheduler();
        scheduler.Register(CreateGame("game-1"));

        Assert.Throws<InvalidOperationException>(() => scheduler.Register(CreateGame("game-1")));
    }

    [Fact]
    public void ScheduleCycle_ExecutesAtMostQuantumCommandsPerGamePerCycle()
    {
        var scheduler = CreateScheduler();
        var game = CreateGame("game-1");
        QueueCommands(game, 10);
        scheduler.Register(game);

        var firstCycle = scheduler.ScheduleCycle(TestTimestamp);
        var secondCycle = scheduler.ScheduleCycle(TestTimestamp);
        var thirdCycle = scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(CommandQuantum, firstCycle.DispatchedCommandCount);
        Assert.Equal(CommandQuantum, secondCycle.DispatchedCommandCount);
        Assert.Equal(10 - CommandQuantum * 2, thirdCycle.DispatchedCommandCount);
        Assert.Empty(firstCycle.TerminatedGameIds);
    }

    [Fact]
    public void ScheduleCycle_DoesNotDispatch_GamesWithoutQueuedCommands()
    {
        var scheduler = CreateScheduler();
        scheduler.Register(CreateGame("game-1"));

        var scheduleOutcome = scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(0, scheduleOutcome.DispatchedCommandCount);
    }

    [Fact]
    public void ScheduleCycle_ServesHighPriorityGamesBeforeNormalPriorityGames()
    {
        var scheduler = CreateScheduler();
        var normalGame = CreateGame("game-normal", GamePriority.Normal);
        var highGame = CreateGame("game-high", GamePriority.High);
        normalGame.CommandQueue.Enqueue(new LogCommand("normal", _executionLog));
        highGame.CommandQueue.Enqueue(new LogCommand("high", _executionLog));
        scheduler.Register(normalGame);
        scheduler.Register(highGame);

        scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(new[] { "high", "normal" }, _executionLog);
    }

    [Fact]
    public void ScheduleCycle_KeepsServingNormalPriorityGames_EveryCycleWithoutStarvation()
    {
        var scheduler = CreateScheduler();
        var highGame = CreateGame("game-high", GamePriority.High);
        var normalGame = CreateGame("game-normal", GamePriority.Normal);
        QueueCommands(highGame, CommandQuantum * 3);
        QueueCommands(normalGame, CommandQuantum * 3);
        scheduler.Register(highGame);
        scheduler.Register(normalGame);

        var firstCycle = scheduler.ScheduleCycle(TestTimestamp);
        var secondCycle = scheduler.ScheduleCycle(TestTimestamp);
        var thirdCycle = scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(CommandQuantum * 2, firstCycle.DispatchedCommandCount);
        Assert.Equal(CommandQuantum * 2, secondCycle.DispatchedCommandCount);
        Assert.Equal(CommandQuantum * 2, thirdCycle.DispatchedCommandCount);
        Assert.Equal(0, scheduler.FindGame("game-normal")!.CommandQueue.Count);
        Assert.Equal(0, scheduler.FindGame("game-high")!.CommandQueue.Count);
    }

    [Fact]
    public void NotifyCommandsQueued_ExcludesAwaitingGame_FromReadyRotation()
    {
        var scheduler = CreateScheduler();
        var game = CreateGame("game-1");
        game.SuspendForExternalEvent(TestTimestamp);
        scheduler.Register(game);
        game.CommandQueue.Enqueue(new LogCommand("suspended", _executionLog));

        scheduler.NotifyCommandsQueued(game);
        var scheduleOutcome = scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(0, scheduleOutcome.DispatchedCommandCount);
        Assert.Empty(_executionLog);
    }

    [Fact]
    public void ScheduleCycle_ResumesDispatch_AfterGameResumesFromAwaiting()
    {
        var scheduler = CreateScheduler();
        var game = CreateGame("game-1");
        game.SuspendForExternalEvent(TestTimestamp);
        scheduler.Register(game);
        game.CommandQueue.Enqueue(new LogCommand("resumed", _executionLog));

        game.ResumeFromExternalEvent();
        scheduler.NotifyCommandsQueued(game);
        var scheduleOutcome = scheduler.ScheduleCycle(TestTimestamp);

        Assert.Equal(1, scheduleOutcome.DispatchedCommandCount);
        Assert.Equal(new[] { "resumed" }, _executionLog);
    }

    [Fact]
    public void ScheduleCycle_KeepsAwaitingGame_WhenAwaitDurationReachesThresholdExactly()
    {
        var scheduler = CreateSchedulerWithAwaitThreshold(TimeSpan.FromMinutes(5));
        var game = CreateGame("game-1");
        game.SuspendForExternalEvent(TestTimestamp);
        scheduler.Register(game);

        scheduler.ScheduleCycle(TestTimestamp.AddMinutes(5));

        Assert.NotNull(scheduler.FindGame("game-1"));
    }

    [Fact]
    public void ScheduleCycle_TerminatesAwaitingGame_AfterAwaitThresholdIsExceeded()
    {
        var scheduler = CreateSchedulerWithAwaitThreshold(TimeSpan.FromMinutes(5));
        var game = CreateGame("game-1");
        game.SuspendForExternalEvent(TestTimestamp);
        scheduler.Register(game);

        var scheduleOutcome = scheduler.ScheduleCycle(TestTimestamp.AddMinutes(5).AddMilliseconds(1));

        Assert.Equal(new[] { "game-1" }, scheduleOutcome.TerminatedGameIds);
        Assert.Null(scheduler.FindGame("game-1"));
        Assert.Equal(("game-1", GameTerminationReason.WatchdogAwaitTimeout), _terminationObserver.SingleTermination);
    }

    [Fact]
    public void ScheduleCycle_TerminatesOnlyExpiredAwaitingGames()
    {
        var scheduler = CreateSchedulerWithAwaitThreshold(TimeSpan.FromMinutes(5));
        var expiredGame = CreateGame("game-expired");
        var freshGame = CreateGame("game-fresh");
        expiredGame.SuspendForExternalEvent(TestTimestamp);
        freshGame.SuspendForExternalEvent(TestTimestamp.AddMinutes(4));
        scheduler.Register(expiredGame);
        scheduler.Register(freshGame);

        var scheduleOutcome = scheduler.ScheduleCycle(TestTimestamp.AddMinutes(6));

        Assert.Equal(new[] { "game-expired" }, scheduleOutcome.TerminatedGameIds);
        Assert.NotNull(scheduler.FindGame("game-fresh"));
    }

    [Fact]
    public void Unregister_RemovesGame_AndNotifiesObserverWithProvidedReason()
    {
        var scheduler = CreateScheduler();
        scheduler.Register(CreateGame("game-1"));

        scheduler.Unregister("game-1", GameTerminationReason.FloodQueueOverflow);

        Assert.Null(scheduler.FindGame("game-1"));
        Assert.Equal(0, scheduler.RegisteredGameCount);
        Assert.Equal(("game-1", GameTerminationReason.FloodQueueOverflow), _terminationObserver.SingleTermination);
    }

    [Fact]
    public void Unregister_ThrowsKeyNotFoundException_WhenGameIsNotRegistered()
    {
        var scheduler = CreateScheduler();

        Assert.Throws<KeyNotFoundException>(() =>
            scheduler.Unregister("missing", GameTerminationReason.FloodQueueOverflow));
    }

    private PriorityRoundRobinScheduler CreateScheduler()
    {
        return CreateSchedulerWithAwaitThreshold(GameWorkerSettings.CreateDefault().MaxExternalAwaitDuration);
    }

    private PriorityRoundRobinScheduler CreateSchedulerWithAwaitThreshold(TimeSpan maxAwaitDuration)
    {
        var settings = new GameWorkerSettings(
            CommandQuantum,
            256,
            100,
            maxAwaitDuration,
            100,
            100);

        return new PriorityRoundRobinScheduler(
            settings,
            new CommandRateLimiter(settings.RateLimitBucketCapacity, settings.RateLimitTokensPerSecond),
            _terminationObserver);
    }

    private Game CreateGame(string gameId, GamePriority priority = GamePriority.Normal)
    {
        return new Game(gameId, priority, new Scope());
    }

    private void QueueCommands(Game game, int commandCount)
    {
        Enumerable
            .Range(0, commandCount)
            .ToList()
            .ForEach(_ => game.CommandQueue.Enqueue(new LogCommand(game.GameId, _executionLog)));
    }

    private sealed class LogCommand(string marker, List<string> executionLog) : ICommand
    {
        public void Execute()
        {
            executionLog.Add(marker);
        }
    }

    private sealed class RecordingGameTerminationObserver : IGameTerminationObserver
    {
        public List<(string GameId, GameTerminationReason Reason)> Terminations { get; } = [];

        public (string GameId, GameTerminationReason Reason)? SingleTermination =>
            Terminations.Count == 1 ? Terminations[0] : null;

        public void NotifyGameTerminated(string gameId, GameTerminationReason terminationReason)
        {
            Terminations.Add((gameId, terminationReason));
        }
    }
}
