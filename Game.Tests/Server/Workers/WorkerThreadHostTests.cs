namespace Game.Tests.Server.Workers;

using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;
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
}
