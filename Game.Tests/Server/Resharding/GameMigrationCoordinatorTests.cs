using Game;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;

namespace Game.Tests.Server.Resharding;

public class GameMigrationCoordinatorTests
{
    [Fact]
    public void Migration_WhenTargetIsMissing_PreservesSourceGame()
    {
        var now = DateTimeOffset.UtcNow;
        var queue = new BoundedMessageQueue<InboundMessage>(16);
        var settings = GameWorkerSettings.CreateDefault();
        var scheduler = new PriorityRoundRobinScheduler(settings, new CommandRateLimiter(16, 64), NullGameTerminationObserver.Instance);
        var services = new GameWorkerSupportServices(new JsonServerSnapshotCodec(),
            new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>()),
            new RecordingOverflowSink(), new InMemorySnapshotStore());
        var worker = new GameWorker(queue, scheduler, new JsonServerCommandDeserializer(TimeProvider.System),
            new DefaultGameFactory(), settings, services);
        queue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.CreateItem,
            """{"itemId":"ship1","type":"Ship","position":[1,2],"speed":[3,4]}"""));
        worker.ExecuteCycle(now);
        queue.TryWrite(new InboundMessage(0, "game-1", "system", ServerCommandNames.MigrateOut,
            """{"targetThreadId":1}"""));

        worker.ExecuteCycle(now.AddSeconds(1));

        Assert.True(scheduler.FindGame("game-1")?.Scope.Contains("ship1"));
    }

    [Fact]
    public void BeginThreadMigration_UpdatesReshardingTable_AndPostsMigrateOutToSourceWorker()
    {
        var reshardingTable = new ReshardingTable(2);
        var postedMessages = new List<(int ThreadId, InboundMessage Message)>();
        var messagePoster = new RecordingMessagePoster(postedMessages);
        var migrationCoordinator = new GameMigrationCoordinator(reshardingTable, messagePoster);

        migrationCoordinator.BeginThreadMigration(new GameMigrationRequest("game-1", 0, 1));

        Assert.Equal(1, reshardingTable.ResolveRoute("game-1"));
        var (postedThreadId, postedMessage) = Assert.Single(postedMessages);
        Assert.Equal(0, postedThreadId);
        Assert.Equal("game-1", postedMessage.GameId);
        Assert.Equal(ServerCommandNames.MigrateOut, postedMessage.CommandName);
        Assert.Contains("\"targetThreadId\":1", postedMessage.CommandArguments);
    }

    [Fact]
    public void BeginThreadMigration_ThrowsInvalidOperationException_WhenSourceWorkerRejectsPosting()
    {
        var routes = new ReshardingTable(2);
        routes.SetRoute("game-1", 0);
        var migrationCoordinator = new GameMigrationCoordinator(routes, new RejectingMessagePoster());

        Assert.Throws<InvalidOperationException>(() =>
            migrationCoordinator.BeginThreadMigration(new GameMigrationRequest("game-1", 0, 1)));
        Assert.Equal(0, routes.ResolveRoute("game-1"));
    }

    [Fact]
    public void Migration_PostTickMessagesOnSourceWorker_AreForwardedToTargetWorker_NotDropped()
    {
        var testTimestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var sourceInputQueue = new BoundedMessageQueue<InboundMessage>(16);
        var targetInputQueue = new BoundedMessageQueue<InboundMessage>(16);
        var transitResolver = new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>
        {
            [1] = targetInputQueue
        });
        var overflowSink = new RecordingOverflowSink();
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            transitResolver,
            overflowSink,
            new InMemorySnapshotStore());

        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);

        var worker = new GameWorker(
            sourceInputQueue,
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(testTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);

        sourceInputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.CreateItem,
            """{"itemId":"ship1","type":"Ship","position":[1,2],"speed":[3,4]}"""));
        worker.ExecuteCycle(testTimestamp);
        Assert.NotNull(scheduler.FindGame("game-1"));

        sourceInputQueue.TryWrite(new InboundMessage(0, "game-1", "system", ServerCommandNames.MigrateOut,
            """{"targetThreadId":1}"""));
        sourceInputQueue.TryWrite(new InboundMessage(0, "game-1", "token", ServerCommandNames.Move,
            """{"itemId":"ship1"}"""));

        var result = worker.ExecuteCycle(testTimestamp.AddSeconds(1));

        Assert.Equal(0, result.RejectedMessageCount);
        Assert.True(targetInputQueue.PendingCount >= 1);
        var forwardedMessage = targetInputQueue.ReadBlocking(CancellationToken.None);
        Assert.Equal("game-1", forwardedMessage.GameId);
        Assert.Equal(ServerCommandNames.Move, forwardedMessage.CommandName);
    }

    [Fact]
    public void Migration_WhenClientMessageArrivesAtTargetBeforeMigrateIn_SnapshotIsMergedAndNotDiscarded()
    {
        var testTimestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var targetInputQueue = new BoundedMessageQueue<InboundMessage>(16);
        var overflowSink = new RecordingOverflowSink();
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>()),
            overflowSink,
            new InMemorySnapshotStore());

        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);

        var targetWorker = new GameWorker(
            targetInputQueue,
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(testTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);

        targetInputQueue.TryWrite(new InboundMessage(1, "game-1", "token", ServerCommandNames.Move,
            """{"itemId":"ship1"}"""));
        targetWorker.ExecuteCycle(testTimestamp);

        var initialGame = scheduler.FindGame("game-1");
        Assert.NotNull(initialGame);
        Assert.False(initialGame.Scope.Contains("ship1"));

        const string snapshotJson = """
        {
            "gameId": "game-1",
            "priority": "Normal",
            "items": [
                {
                    "itemId": "ship1",
                    "properties": {"Type":"Ship","Position":[10,20],"Speed":[1,2]}
                }
            ]
        }
        """;
        targetInputQueue.TryWrite(new InboundMessage(1, "game-1", "system", ServerCommandNames.MigrateIn, snapshotJson));
        var cycleResult = targetWorker.ExecuteCycle(testTimestamp.AddSeconds(1));

        Assert.Equal(0, cycleResult.RejectedMessageCount);
        var gameAfterMigration = scheduler.FindGame("game-1");
        Assert.NotNull(gameAfterMigration);
        Assert.True(gameAfterMigration.Scope.Contains("ship1"));
        var item = gameAfterMigration.Scope.GetItem("ship1");
        Assert.Equal(new Vector([10, 20]), item.GetProperty("Position"));
    }

    [Fact]
    public void GameMigrationCoordinator_ImplementsIGameMigrationCoordinator_Abstraction()
    {
        var reshardingTable = new ReshardingTable(2);
        var migrationPoster = new RecordingMessagePoster([]);
        var migrationCoord = new GameMigrationCoordinator(reshardingTable, migrationPoster);
        Assert.IsAssignableFrom<IGameMigrationCoordinator>(migrationCoord);
    }

    private sealed class TestTransitResolver(IReadOnlyDictionary<int, IMessageQueueWriter<InboundMessage>> writers) : ITransitResolver
    {
        public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId)
        {
            if (writers.TryGetValue(threadId, out var writer))
            {
                return writer;
            }

            throw new KeyNotFoundException($"Worker '{threadId}' not found.");
        }
    }

    private sealed class RecordingMessagePoster(List<(int ThreadId, InboundMessage Message)> postedMessages) : IWorkerMessagePoster
    {
        public bool TryPost(int threadId, InboundMessage message)
        {
            postedMessages.Add((threadId, message));
            return true;
        }
    }

    private sealed class RejectingMessagePoster : IWorkerMessagePoster
    {
        public bool TryPost(int threadId, InboundMessage message)
        {
            return false;
        }
    }
}
