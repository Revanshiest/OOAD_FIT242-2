namespace Game.Tests.Server.Workers;

using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;
using Game.Tests.Server.Resharding;
using Game = GameServer.Games.Game;

public class GameWorkerTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const int InputQueueCapacity = 64;

    private readonly RecordingGameTerminationObserver _terminationObserver = new();

    [Fact]
    public void ExecuteCycle_RoutesCreateItemMessage_AndRunsFullCreationPipeline()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 0, 1 }));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        var createdGame = scheduler.FindGame("game-1");
        Assert.Equal(1, cycleResult.RoutedMessageCount);
        Assert.Equal(0, cycleResult.RejectedMessageCount);
        Assert.NotNull(createdGame);
        Assert.Equal("Ship", createdGame!.Scope.GetItem("ship1").GetProperty(GameItemProperty.DomainType));
        Assert.Equal(new Vector(new[] { 1, 2 }), createdGame!.Scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
        Assert.Equal(new Vector(new[] { 0, 1 }), createdGame!.Scope.GetItem("ship1").GetProperty(GameItemProperty.Speed));
    }

    [Fact]
    public void ExecuteCycle_AppliesMoveCommand_AfterItemCreation()
    {
        var (worker, inputQueue, _) = CreateDefaultWorker();
        inputQueue.TryWrite(CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 3, 4 }));
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));

        worker.ExecuteCycle(TestTimestamp);

        var createdGame = FindCreatedGame("game-1");
        Assert.Equal(new Vector(new[] { 4, 6 }), createdGame!.Scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
    }

    [Fact]
    public void ExecuteCycle_RejectsMessageWithMissingCommandArguments()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.Move, """{"wrongField":"x"}"""));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(0, cycleResult.RoutedMessageCount);
        Assert.Equal(1, cycleResult.RejectedMessageCount);
        Assert.NotNull(scheduler.FindGame("game-1"));
    }

    [Fact]
    public void ExecuteCycle_RejectsMessageWithUnknownCommandName()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", "Teleport", "{}"));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(0, cycleResult.RoutedMessageCount);
        Assert.Equal(1, cycleResult.RejectedMessageCount);
        Assert.NotNull(scheduler.FindGame("game-1"));
    }

    [Fact]
    public void ExecuteCycle_RejectsMessageWithMalformedJsonArguments()
    {
        var (worker, inputQueue, _) = CreateDefaultWorker();
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.CreateItem, "not-a-json"));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(1, cycleResult.RejectedMessageCount);
    }

    [Fact]
    public void ExecuteCycle_RejectsGamesBeyondMaxActiveGamesLimit()
    {
        var settings = new GameWorkerSettings(8, 256, 1, TimeSpan.FromMinutes(5), 100, 100);
        var (worker, inputQueue, scheduler) = CreateWorker(settings);
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        inputQueue.TryWrite(MoveMessage("game-2", "ship1"));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(1, cycleResult.RoutedMessageCount);
        Assert.Equal(1, cycleResult.RejectedMessageCount);
        Assert.Equal(1, cycleResult.FailedCommandCount);
        Assert.Equal(1, scheduler.RegisteredGameCount);
        Assert.NotNull(scheduler.FindGame("game-1"));
        Assert.Null(scheduler.FindGame("game-2"));
    }

    [Fact]
    public void ExecuteCycle_TerminatesSpammingGame_WhenQueueLengthExceedsLimit()
    {
        var settings = new GameWorkerSettings(8, 2, 100, TimeSpan.FromMinutes(5), 100, 100);
        var (worker, inputQueue, scheduler) = CreateWorker(settings);
        Enumerable
            .Range(0, 3)
            .ToList()
            .ForEach(_ => inputQueue.TryWrite(MoveMessage("game-1", "ship1")));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(2, cycleResult.RoutedMessageCount);
        Assert.Equal(1, cycleResult.RejectedMessageCount);
        Assert.Null(scheduler.FindGame("game-1"));
        Assert.Equal(("game-1", GameTerminationReason.FloodQueueOverflow), _terminationObserver.SingleTermination);
    }

    [Fact]
    public void ExecuteCycle_SealsTargetedGame_OnTickMarker_AndRejectsLaterCommandsForIt()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "system", ServerCommandNames.TickMarker, """{"gameId":"game-1"}"""));

        var firstCycle = worker.ExecuteCycle(TestTimestamp);
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        var secondCycle = worker.ExecuteCycle(TestTimestamp.AddSeconds(1));

        Assert.Equal(3, firstCycle.RoutedMessageCount);
        Assert.Equal(0, firstCycle.RejectedMessageCount);
        Assert.Equal(1, firstCycle.DispatchedCommandCount);
        Assert.Equal(2, firstCycle.FailedCommandCount);
        Assert.Equal(1, secondCycle.RejectedMessageCount);
        Assert.Equal(0, secondCycle.DispatchedCommandCount);
        Assert.NotNull(scheduler.FindGame("game-1"));
        Assert.True(FindCreatedGame("game-1")!.CommandQueue.IsSealedByTickMarker);
    }

    [Fact]
    public void ExecuteCycle_SealsWholeWorker_OnGlobalTickMarker_AndSavesFinalSnapshots()
    {
        var (worker, inputQueue, _) = CreateDefaultWorker();
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        inputQueue.TryWrite(new InboundMessage(0, string.Empty, "system", ServerCommandNames.TickMarker, "{}"));
        inputQueue.TryWrite(MoveMessage("game-2", "ship1"));

        var cycleResult = worker.ExecuteCycle(TestTimestamp);

        Assert.Equal(2, cycleResult.RoutedMessageCount);
        Assert.Equal(1, cycleResult.RejectedMessageCount);
        Assert.True(worker.IsShutdownCompleted);
    }

    [Fact]
    public void ExecuteCycle_MigratesGameToTargetWorker_AndInstallsProxyForLateMessages()
    {
        var sourceInputQueue = new BoundedMessageQueue<InboundMessage>(InputQueueCapacity);
        var targetInputQueue = new BoundedMessageQueue<InboundMessage>(InputQueueCapacity);
        var sourceScheduler = CreateScheduler(GameWorkerSettings.CreateDefault());
        var targetScheduler = CreateScheduler(GameWorkerSettings.CreateDefault());
        var transitResolver = new FakeTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>
        {
            [0] = sourceInputQueue,
            [1] = targetInputQueue
        });
        var overflowSink = new RecordingOverflowSink();
        var sourceWorker = CreateWorkerWithServices(sourceInputQueue, sourceScheduler, transitResolver, overflowSink);
        var targetWorker = CreateWorkerWithServices(targetInputQueue, targetScheduler, transitResolver, overflowSink);

        sourceInputQueue.TryWrite(CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 3, 4 }));
        sourceWorker.ExecuteCycle(TestTimestamp);

        sourceInputQueue.TryWrite(new InboundMessage(0, "game-1", "system", ServerCommandNames.MigrateOut, """{"targetThreadId":1}"""));
        sourceWorker.ExecuteCycle(TestTimestamp.AddSeconds(1));

        Assert.Null(sourceScheduler.FindGame("game-1"));
        Assert.Equal(1, targetInputQueue.PendingCount);

        targetWorker.ExecuteCycle(TestTimestamp.AddSeconds(1));
        var restoredGame = targetScheduler.FindGame("game-1");

        Assert.NotNull(restoredGame);
        Assert.True(restoredGame.Scope.Contains("ship1"));
        Assert.Equal(new Vector(new[] { 1, 2 }), restoredGame.Scope.GetItem("ship1").GetProperty(GameItemProperty.Position));

        sourceInputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        sourceWorker.ExecuteCycle(TestTimestamp.AddSeconds(2));
        targetWorker.ExecuteCycle(TestTimestamp.AddSeconds(2));

        Assert.Equal(
            new Vector(new[] { 4, 6 }),
            targetScheduler.FindGame("game-1")!.Scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
    }

    [Fact]
    public void ExecuteCycle_SuspendsGame_OnSuspendCommand_AndResumesOnResumeMessage()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(MoveMessage("game-1", "ship1"));
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.Suspend, "{}"));
        worker.ExecuteCycle(TestTimestamp);

        var suspendedGame = FindCreatedGame("game-1");
        Assert.NotNull(suspendedGame);
        Assert.True(suspendedGame.IsAwaitingExternalEvent);

        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.Resume, "{}"));
        worker.ExecuteCycle(TestTimestamp.AddSeconds(1));

        var resumedGame = FindCreatedGame("game-1");
        Assert.NotNull(resumedGame);
        Assert.False(resumedGame.IsAwaitingExternalEvent);
    }

    [Fact]
    public void ExecuteCycle_TerminatesGame_AwaitingLongerThanWatchdogThreshold()
    {
        var (worker, inputQueue, scheduler) = CreateDefaultWorker();
        inputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.Suspend, "{}"));
        worker.ExecuteCycle(TestTimestamp);

        var cycleResult = worker.ExecuteCycle(TestTimestamp.AddMinutes(6));

        Assert.Equal(new[] { "game-1" }, cycleResult.TerminatedGameIds);
        Assert.Null(scheduler.FindGame("game-1"));
        Assert.Equal(("game-1", GameTerminationReason.WatchdogAwaitTimeout), _terminationObserver.SingleTermination);
    }

    private Game? FindCreatedGame(string gameId)
    {
        return _terminationObserver.SchedulerRef.FindGame(gameId);
    }

    private static InboundMessage CreateItemMessage(
        string gameId,
        string itemId,
        string domainType,
        int[] position,
        int[] speed)
    {
        var arguments = $$"""{"itemId":"{{itemId}}","type":"{{domainType}}","position":[{{position[0]}},{{position[1]}}],"speed":[{{speed[0]}},{{speed[1]}}]}""";

        return new InboundMessage(0, gameId, "token", ServerCommandNames.CreateItem, arguments);
    }

    private static InboundMessage MoveMessage(string gameId, string itemId)
    {
        return new InboundMessage(0, gameId, "token", ServerCommandNames.Move, $$"""{"itemId":"{{itemId}}"}""");
    }

    private (GameWorker Worker, BoundedMessageQueue<InboundMessage> InputQueue, PriorityRoundRobinScheduler Scheduler) CreateDefaultWorker()
    {
        return CreateWorker(GameWorkerSettings.CreateDefault());
    }

    private PriorityRoundRobinScheduler CreateScheduler(GameWorkerSettings settings)
    {
        return new PriorityRoundRobinScheduler(
            settings,
            new CommandRateLimiter(settings.RateLimitBucketCapacity, settings.RateLimitTokensPerSecond),
            _terminationObserver);
    }

    private GameWorker CreateWorkerWithServices(
        BoundedMessageQueue<InboundMessage> inputQueue,
        PriorityRoundRobinScheduler scheduler,
        FakeTransitResolver transitResolver,
        RecordingOverflowSink overflowSink)
    {
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            transitResolver,
            overflowSink,
            new InMemorySnapshotStore());

        return new GameWorker(
            inputQueue,
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);
    }

    private (GameWorker Worker, BoundedMessageQueue<InboundMessage> InputQueue, PriorityRoundRobinScheduler Scheduler) CreateWorker(GameWorkerSettings settings)
    {
        var inputQueue = new BoundedMessageQueue<InboundMessage>(InputQueueCapacity);
        var scheduler = new PriorityRoundRobinScheduler(
            settings,
            new CommandRateLimiter(settings.RateLimitBucketCapacity, settings.RateLimitTokensPerSecond),
            _terminationObserver);
        var transitResolver = new FakeTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>> { [0] = inputQueue });
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            transitResolver,
            new RecordingOverflowSink(),
            new InMemorySnapshotStore());
        var worker = new GameWorker(
            inputQueue,
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)),
            new DefaultGameFactory(),
            settings,
            supportServices);

        _terminationObserver.SchedulerRef = scheduler;

        return (worker, inputQueue, scheduler);
    }

    private sealed class RecordingGameTerminationObserver : IGameTerminationObserver
    {
        public PriorityRoundRobinScheduler SchedulerRef { get; set; } = null!;

        public List<(string GameId, GameTerminationReason Reason)> Terminations { get; } = [];

        public (string GameId, GameTerminationReason Reason)? SingleTermination =>
            Terminations.Count == 1 ? Terminations[0] : null;

        public void NotifyGameTerminated(string gameId, GameTerminationReason terminationReason)
        {
            Terminations.Add((gameId, terminationReason));
        }
    }
}
