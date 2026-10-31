namespace Game.Tests.Server.Scaling;

using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Scaling;
using GameServer.Workers;
using Game.Tests.Server.Resharding;

public class WorkerPoolTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RecordingOverflowSink _overflowSink = new();
    private readonly InMemorySnapshotStore _snapshotStore = new();

    [Fact]
    public void StartWorker_RegistersWorker_AndCollectsStatistics()
    {
        var workerPool = CreateWorkerPool();

        var threadId = workerPool.StartWorker();

        Assert.Equal(0, threadId);
        Assert.Equal(1, workerPool.WorkerCount);
        Assert.Equal(new[] { new WorkerStatistics(0, 0, 0, 0) }, workerPool.CollectStatistics());
        workerPool.StopAll();
    }

    [Fact]
    public void StartWorker_AssignsSequentialThreadIdentifiers()
    {
        var workerPool = CreateWorkerPool();

        var firstThreadId = workerPool.StartWorker();
        var secondThreadId = workerPool.StartWorker();

        Assert.Equal(0, firstThreadId);
        Assert.Equal(1, secondThreadId);
        workerPool.StopAll();
    }

    [Fact]
    public void TryPost_ReturnsFalse_ForUnknownWorker()
    {
        var workerPool = CreateWorkerPool();

        var messagePosted = workerPool.TryPost(9, MoveMessage("game-1", "ship1"));

        Assert.False(messagePosted);
    }

    [Fact]
    public void ResolveWriter_ThrowsKeyNotFoundException_ForUnknownWorker()
    {
        var workerPool = CreateWorkerPool();

        Assert.Throws<KeyNotFoundException>(() => workerPool.ResolveWriter(9));
    }

    [Fact]
    public void StopWorker_RemovesWorker_AndRejectsRepeatedStop()
    {
        var workerPool = CreateWorkerPool();
        var threadId = workerPool.StartWorker();

        workerPool.StopWorker(threadId);

        Assert.Equal(0, workerPool.WorkerCount);
        Assert.Throws<KeyNotFoundException>(() => workerPool.StopWorker(threadId));
    }

    [Fact]
    public void StartedWorkers_ExecuteClientCommands_AndReshardingTableStaysConsistentAfterMigration()
    {
        var workerPool = CreateWorkerPool();
        workerPool.StartWorker();
        workerPool.StartWorker();
        var reshardingTable = new ReshardingTable(2);
        var migrationCoordinator = new GameMigrationCoordinator(reshardingTable, workerPool);

        workerPool.TryPost(0, CreateItemMessage("game-1", "ship1", "Ship", new[] { 1, 2 }, new[] { 3, 4 }));
        var gameCreated = ConditionWaiter.WaitUntil(() =>
            workerPool.TryGetGameScope(0, "game-1")?.Contains("ship1") == true);

        migrationCoordinator.BeginThreadMigration(new GameMigrationRequest("game-1", 0, 1));
        var gameMigrated = ConditionWaiter.WaitUntil(() =>
            workerPool.TryGetGameScope(0, "game-1") is null
            && workerPool.TryGetGameScope(1, "game-1")?.Contains("ship1") == true);

        workerPool.TryPost(0, MoveMessage("game-1", "ship1"));
        var lateMessageForwardedThroughProxy = ConditionWaiter.WaitUntil(() =>
            workerPool.TryGetGameScope(1, "game-1")?.GetItem("ship1").GetProperty(GameItemProperty.Position)
            is Vector { } movedPosition
            && movedPosition.Equals(new Vector(new[] { 4, 6 })));

        workerPool.StopAll();

        Assert.True(gameCreated);
        Assert.True(gameMigrated);
        Assert.True(lateMessageForwardedThroughProxy);
        Assert.Equal(1, reshardingTable.ResolveRoute("game-1"));
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
