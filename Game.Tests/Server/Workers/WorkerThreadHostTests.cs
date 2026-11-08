namespace Game.Tests.Server.Workers;

using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;
using Game.Tests.Server.Messaging;
using Game.Tests.Server.Resharding;

public class WorkerThreadHostTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartedWorkerThread_ProcessesIncomingMessages_UntilStopIsRequested()
    {
        var inputQueue = new BoundedMessageQueue<InboundMessage>(64);
        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(100, 100),
            NullGameTerminationObserver.Instance);
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
            GameWorkerSettings.CreateDefault(),
            supportServices);
        var workerHost = new WorkerThreadHost(7, worker, inputQueue, new FixedTimeProvider(TestTimestamp));

        workerHost.Start();
        workerHost.MessageWriter.TryWrite(new InboundMessage(
            7,
            "game-live",
            "token",
            ServerCommandNames.CreateItem,
            """{"itemId":"ship1","type":"Ship","position":[0,0],"speed":[1,1]}"""));

        var gameProcessedMessage = ConditionWaiter.WaitUntil(() =>
            scheduler.FindGame("game-live") is not null
            && scheduler.FindGame("game-live")!.Scope.Contains("ship1"));

        workerHost.Stop();
        workerHost.Join();

        Assert.Equal(7, workerHost.ThreadId);
        Assert.True(gameProcessedMessage);
    }

    [Fact]
    public void WorkerThreadHost_WorksWithIGameWorker_Abstraction()
    {
        var mockWorker = new MockGameWorker();
        var writer = new RecordingMessageWriter();
        var host = new WorkerThreadHost(0, mockWorker, writer, TimeProvider.System);

        Assert.IsAssignableFrom<IWorkerThreadHost>(host);
        host.Start();
        Thread.Sleep(20);
        host.Stop();
        host.Join();

        Assert.True(mockWorker.ExecuteCycleCalled);
    }

    private sealed class MockGameWorker : IGameWorker
    {
        public bool IsShutdownCompleted => false;
        public bool HasReadyWork => false;
        public bool ExecuteCycleCalled { get; private set; }

        public WorkerCycleResult ExecuteCycle(DateTimeOffset currentTime)
        {
            ExecuteCycleCalled = true;
            return new WorkerCycleResult(0, 0, 0, 0, []);
        }

        public bool WaitForIncomingMessage(TimeSpan waitTimeout, CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne(waitTimeout);
            return false;
        }
    }
}
