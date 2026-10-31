using GameServer.Endpoint;
using GameServer.Lifecycle;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Scaling;
using GameServer.Workers;
using Game.Tests.Server.Resharding;

namespace Game.Tests.Server;

public class ConstructorGuardTests
{
    private static GameWorker CreateWorker(GameWorkerSupportServices? supportServices = null) =>
        new(
            new BoundedMessageQueue<InboundMessage>(16),
            new PriorityRoundRobinScheduler(
                GameWorkerSettings.CreateDefault(),
                new CommandRateLimiter(16, 64),
                NullGameTerminationObserver.Instance),
            new JsonServerCommandDeserializer(TimeProvider.System),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices ?? CreateSupportServices());

    private static GameWorkerSupportServices CreateSupportServices() =>
        new(
            new JsonServerSnapshotCodec(),
            new RecordingTransitResolver(),
            new RecordingOverflowSink(),
            new InMemorySnapshotStore());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void GameWorker_Constructor_RejectsNullDependencies(int nullParameterIndex)
    {
        var supportServices = nullParameterIndex == 5 ? null! : CreateSupportServices();
        var inputReader = nullParameterIndex == 0 ? null! : new BoundedMessageQueue<InboundMessage>(16);
        var scheduler = nullParameterIndex == 1
            ? null!
            : new PriorityRoundRobinScheduler(
                GameWorkerSettings.CreateDefault(),
                new CommandRateLimiter(16, 64),
                NullGameTerminationObserver.Instance);
        var deserializer = nullParameterIndex == 2 ? null! : new JsonServerCommandDeserializer(TimeProvider.System);
        var factory = nullParameterIndex == 3 ? null! : new DefaultGameFactory();
        var settings = nullParameterIndex == 4 ? null! : GameWorkerSettings.CreateDefault();

        Assert.Throws<ArgumentNullException>(() => new GameWorker(
            inputReader, scheduler, deserializer, factory, settings, supportServices));
    }

    [Fact]
    public void PriorityRoundRobinScheduler_Constructor_RejectsNullDependencies()
    {
        var settings = GameWorkerSettings.CreateDefault();
        var rateLimiter = new CommandRateLimiter(16, 64);

        Assert.Throws<ArgumentNullException>(() =>
            new PriorityRoundRobinScheduler(null!, rateLimiter, NullGameTerminationObserver.Instance));
        Assert.Throws<ArgumentNullException>(() =>
            new PriorityRoundRobinScheduler(settings, null!, NullGameTerminationObserver.Instance));
        Assert.Throws<ArgumentNullException>(() =>
            new PriorityRoundRobinScheduler(settings, rateLimiter, null!));
    }

    [Fact]
    public void JsonServerCommandDeserializer_Constructor_RejectsNullTimeProvider()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonServerCommandDeserializer(null!));
    }

    [Fact]
    public void Endpoint_Constructor_RejectsNullDependencies()
    {
        var authenticator = new WhitelistTokenAuthenticator(["token"]);
        var parser = new InboundMessageParser();
        var router = new PrimaryRouter(
            [new BoundedMessageQueue<InboundMessage>(16)],
            new ReshardingTable(1));
        var rateLimiter = PermissiveRateLimiter.Instance;
        var timeProvider = TimeProvider.System;

        Assert.Throws<ArgumentNullException>(() =>
            new Endpoint(null!, parser, router, rateLimiter, timeProvider));
        Assert.Throws<ArgumentNullException>(() =>
            new Endpoint(authenticator, null!, router, rateLimiter, timeProvider));
        Assert.Throws<ArgumentNullException>(() =>
            new Endpoint(authenticator, parser, null!, rateLimiter, timeProvider));
        Assert.Throws<ArgumentNullException>(() =>
            new Endpoint(authenticator, parser, router, null!, timeProvider));
        Assert.Throws<ArgumentNullException>(() =>
            new Endpoint(authenticator, parser, router, rateLimiter, null!));
    }

    [Fact]
    public void WorkerPool_Constructor_RejectsNullDependencies()
    {
        var settings = GameWorkerSettings.CreateDefault();
        var timeProvider = TimeProvider.System;
        var observer = NullGameTerminationObserver.Instance;
        var deserializer = new JsonServerCommandDeserializer(TimeProvider.System);
        var factory = new DefaultGameFactory();
        var codec = new JsonServerSnapshotCodec();
        var overflowSink = new RecordingOverflowSink();
        var store = new InMemorySnapshotStore();

        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(null!, timeProvider, observer, deserializer, factory, codec, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, null!, observer, deserializer, factory, codec, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, null!, deserializer, factory, codec, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, observer, null!, factory, codec, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, observer, deserializer, null!, codec, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, observer, deserializer, factory, null!, overflowSink, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, observer, deserializer, factory, codec, null!, store));
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerPool(settings, timeProvider, observer, deserializer, factory, codec, overflowSink, null!));
    }

    [Fact]
    public void ServerShutdownCoordinator_Constructor_RejectsNullDependencies()
    {
        var workerPool = CreateWorkerPool();
        var snapshotStore = new InMemorySnapshotStore();
        var timeProvider = TimeProvider.System;

        Assert.Throws<ArgumentNullException>(() =>
            new ServerShutdownCoordinator(null!, snapshotStore, timeProvider, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentNullException>(() =>
            new ServerShutdownCoordinator(workerPool, null!, timeProvider, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentNullException>(() =>
            new ServerShutdownCoordinator(workerPool, snapshotStore, null!, TimeSpan.FromSeconds(1)));
    }

    private static WorkerPool CreateWorkerPool()
    {
        return new WorkerPool(
            GameWorkerSettings.CreateDefault(),
            TimeProvider.System,
            NullGameTerminationObserver.Instance,
            new JsonServerCommandDeserializer(TimeProvider.System),
            new DefaultGameFactory(),
            new JsonServerSnapshotCodec(),
            new RecordingOverflowSink(),
            new InMemorySnapshotStore());
    }

    private sealed class RecordingTransitResolver : ITransitResolver
    {
        public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId)
        {
            throw new KeyNotFoundException($"Worker '{threadId}' is not registered in the test transit resolver.");
        }
    }
}
