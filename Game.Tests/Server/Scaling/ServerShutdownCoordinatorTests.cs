namespace Game.Tests.Server.Scaling;

using GameServer.Endpoint;
using GameServer.Lifecycle;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Scaling;
using GameServer.Workers;
using Game.Tests.Server.Messaging;
using Game.Tests.Server.Resharding;

public class ServerShutdownCoordinatorTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RecordingOverflowSink _overflowSink = new();
    private readonly InMemorySnapshotStore _snapshotStore = new();

    [Fact]
    public void SoftStop_SealsWorkers_SavesSnapshots_AndCompletesGracefully()
    {
        var workerPool = CreateWorkerPool();
        workerPool.StartWorker();
        workerPool.TryPost(0, CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 0, 1 }));
        ConditionWaiter.WaitUntil(() => workerPool.TryGetGameScope(0, "game-1")?.Contains("ship1") == true);
        var shutdownCoordinator = new ServerShutdownCoordinator(
            workerPool,
            _snapshotStore,
            TimeProvider.System,
            TimeSpan.FromSeconds(10));

        var shutdownResult = shutdownCoordinator.SoftStop();

        Assert.True(shutdownResult.CompletedGracefully);
        Assert.Equal(1, shutdownResult.SavedSnapshotCount);
        Assert.Equal(0, workerPool.WorkerCount);

        var savedSnapshot = Assert.Single(_snapshotStore.LoadAll());
        Assert.Equal("game-1", savedSnapshot.GameId);
        Assert.Contains("Position", savedSnapshot.Items[0].SerializedProperties);
    }

    [Fact]
    public void SoftStop_SwitchesToHardStop_WhenWorkerCannotFinishWithinTimeout()
    {
        var workerPool = CreateWorkerPool();
        workerPool.StartWorker();
        workerPool.TryPost(0, CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 0, 1 }));
        ConditionWaiter.WaitUntil(() => workerPool.TryGetGameScope(0, "game-1")?.Contains("ship1") == true);
        workerPool.TryPost(0, new InboundMessage(0, "game-1", "system", ServerCommandNames.MigrateOut, """{"targetThreadId":5}"""));
        var shutdownCoordinator = new ServerShutdownCoordinator(
            workerPool,
            _snapshotStore,
            TimeProvider.System,
            TimeSpan.FromMilliseconds(300));

        var shutdownResult = shutdownCoordinator.SoftStop();

        Assert.False(shutdownResult.CompletedGracefully);
        Assert.Equal(0, workerPool.WorkerCount);
    }

    [Fact]
    public void HardStop_StopsWorkersImmediately_WithoutWaitingForDrain()
    {
        var workerPool = CreateWorkerPool();
        workerPool.StartWorker();
        var shutdownCoordinator = new ServerShutdownCoordinator(
            workerPool,
            _snapshotStore,
            TimeProvider.System,
            TimeSpan.FromSeconds(30));

        var shutdownResult = shutdownCoordinator.HardStop();

        Assert.False(shutdownResult.CompletedGracefully);
        Assert.Equal(0, workerPool.WorkerCount);
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenTimeoutIsNotPositive()
    {
        var workerPool = CreateWorkerPool();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ServerShutdownCoordinator(
            workerPool,
            _snapshotStore,
            TimeProvider.System,
            TimeSpan.Zero));
    }

    [Fact]
    public void SoftStop_DoesNotHangOnAwaitingGames_SavesSnapshotGracefully()
    {
        var workerPool = CreateWorkerPool();
        var threadId = workerPool.StartWorker();

        workerPool.TryPost(threadId, new InboundMessage(threadId, "game-await", "token", ServerCommandNames.CreateItem,
            """{"itemId":"item1","type":"Ship","position":[1,2],"speed":[3,4]}"""));

        ConditionWaiter.WaitUntil(() => workerPool.TryGetGameScope(threadId, "game-await")?.Contains("item1") == true);

        var coordinator = new ServerShutdownCoordinator(
            workerPool,
            _snapshotStore,
            TimeProvider.System,
            TimeSpan.FromSeconds(5));

        var shutdownResult = coordinator.SoftStop();

        Assert.True(shutdownResult.CompletedGracefully);
        Assert.True(shutdownResult.SavedSnapshotCount >= 1);

        var savedSnapshots = _snapshotStore.LoadAll();
        Assert.Contains(savedSnapshots, s => s.GameId == "game-await");
    }

    [Fact]
    public void ServerShutdownCoordinator_Restart_SealsAndResumesAcceptance()
    {
        var writer = new RecordingMessageWriter();
        var primaryRouter = new PrimaryRouter([writer], new ReshardingTable(1));
        var endpoint = new Endpoint(
            new WhitelistTokenAuthenticator(["token"]),
            new InboundMessageParser(),
            primaryRouter);

        var workerPool = CreateWorkerPool();

        var coordinator = new ServerShutdownCoordinator(
            workerPool,
            new InMemorySnapshotStore(),
            TimeProvider.System,
            TimeSpan.FromSeconds(2),
            endpoint);

        Assert.False(endpoint.IsAcceptanceSealed);

        coordinator.SealAcceptance();
        Assert.True(endpoint.IsAcceptanceSealed);

        coordinator.Restart();
        Assert.False(endpoint.IsAcceptanceSealed);
    }

    [Fact]
    public void ServerShutdownCoordinator_ImplementsIServerShutdownCoordinator_Abstraction()
    {
        var workerPool = CreateWorkerPool();
        var shutdownCoord = new ServerShutdownCoordinator(
            workerPool,
            new InMemorySnapshotStore(),
            TimeProvider.System,
            TimeSpan.FromSeconds(5));

        Assert.IsAssignableFrom<IServerShutdownCoordinator>(shutdownCoord);
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

    private WorkerPool CreateWorkerPool()
    {
        return new WorkerPool(
            GameWorkerSettings.CreateDefault(),
            new FixedTimeProvider(TestTimestamp),
            NullGameTerminationObserver.Instance,
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)),
            new DefaultGameFactory(),
            new JsonServerSnapshotCodec(),
            _overflowSink,
            _snapshotStore);
    }
}
