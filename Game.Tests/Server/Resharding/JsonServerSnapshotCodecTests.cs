using Game;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;

namespace Game.Tests.Server.Resharding;

public class JsonServerSnapshotCodecTests
{
    private readonly JsonServerSnapshotCodec _snapshotCodec = new();

    [Fact]
    public void Serialize_And_Deserialize_RestoreSnapshotWithAllItems()
    {
        var sourceSnapshot = new GameSnapshot(
            "game-1",
            GamePriority.High,
            [
                new GameObjectSnapshot("ship1", """{"Type":"Ship","Position":[1,2],"Speed":[0,1]}"""),
                new GameObjectSnapshot("torpedo1", """{"Type":"Torpedo","Damage":5}""")
            ]);

        var restoredSnapshot = _snapshotCodec.Deserialize(_snapshotCodec.Serialize(sourceSnapshot));

        Assert.Equal("game-1", restoredSnapshot.GameId);
        Assert.Equal(GamePriority.High, restoredSnapshot.Priority);
        Assert.Equal(2, restoredSnapshot.Items.Count);
        Assert.Equal("ship1", restoredSnapshot.Items[0].ItemId);
        Assert.Contains("Position", restoredSnapshot.Items[0].SerializedProperties);
        Assert.Equal("torpedo1", restoredSnapshot.Items[1].ItemId);
        Assert.Contains("Damage", restoredSnapshot.Items[1].SerializedProperties);
    }

    [Fact]
    public void Serialize_ProducesJsonObjectPayload()
    {
        var sourceSnapshot = new GameSnapshot(
            "game-1",
            GamePriority.Normal,
            [new GameObjectSnapshot("ship1", """{"Type":"Ship"}""")]);

        var serializedPayload = _snapshotCodec.Serialize(sourceSnapshot);

        Assert.StartsWith("{", serializedPayload);
        Assert.Contains("\"gameId\":\"game-1\"", serializedPayload);
        Assert.Contains("\"priority\":\"Normal\"", serializedPayload);
    }

    [Fact]
    public void Deserialize_ThrowsArgumentException_WhenPriorityIsUnknown()
    {
        const string malformedPayload = """{"gameId":"game-1","priority":"Ultra","items":[]}""";

        Assert.Throws<ArgumentException>(() => _snapshotCodec.Deserialize(malformedPayload));
    }

    [Fact]
    public void Deserialize_ThrowsKeyNotFoundException_WhenRequiredFieldIsMissing()
    {
        const string malformedPayload = """{"priority":"Normal","items":[]}""";

        Assert.Throws<KeyNotFoundException>(() => _snapshotCodec.Deserialize(malformedPayload));
    }

    [Fact]
    public void Serialize_ThrowsArgumentNullException_WhenSnapshotIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _snapshotCodec.Serialize(null!));
    }

    [Fact]
    public void GameSnapshot_SerializesAndDeserializesUnprocessedCommands()
    {
        var snapshot = new GameSnapshot(
            "game-unprocessed",
            GamePriority.High,
            [new GameObjectSnapshot("item1", """{"Type":"Ship"}""")],
            ["MoveCommand", "FireCommand"]);

        var json = _snapshotCodec.Serialize(snapshot);
        Assert.Contains("queuedCommands", json);
        Assert.Contains("MoveCommand", json);
        Assert.Contains("FireCommand", json);

        var restored = _snapshotCodec.Deserialize(json);
        Assert.Equal("game-unprocessed", restored.GameId);
        Assert.Equal(GamePriority.High, restored.Priority);
        Assert.NotNull(restored.QueuedCommands);
        Assert.Equal(2, restored.QueuedCommands.Count);
        Assert.Equal("MoveCommand", restored.QueuedCommands[0]);
        Assert.Equal("FireCommand", restored.QueuedCommands[1]);
    }

    [Fact]
    public void WorkerSnapshot_PreservesAngleBoolAndDoubleTypes()
    {
        var testTimestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var inputQueue = new BoundedMessageQueue<InboundMessage>(16);
        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);
        var snapshotStore = new InMemorySnapshotStore();
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>()),
            new RecordingOverflowSink(),
            snapshotStore);

        var worker = new GameWorker(
            inputQueue,
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(testTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);

        var game = new GameServer.Games.Game("game-types", GamePriority.High, new Scope());
        var item = new GameItem();
        item.SetProperty("Position", new Vector([10, 20]));
        item.SetProperty("Orientation", new Angle(5));
        item.SetProperty("IsAlive", true);
        item.SetProperty("Health", 99.5);
        item.SetProperty("Score", 1000);
        game.Scope.Register("player1", item);

        scheduler.Register(game);
        worker.SecondaryRouter.RegisterRecipient("game-types", game);

        inputQueue.TryWrite(new InboundMessage(0, string.Empty, "system", ServerCommandNames.TickMarker, "{}"));
        worker.ExecuteCycle(testTimestamp);

        Assert.True(worker.IsShutdownCompleted);
        var savedSnapshot = Assert.Single(snapshotStore.LoadAll());

        var targetInputQueue = new BoundedMessageQueue<InboundMessage>(16);
        var targetScheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);
        var targetWorker = new GameWorker(
            targetInputQueue,
            targetScheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(testTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);

        var snapshotJson = _snapshotCodec.Serialize(savedSnapshot);

        targetInputQueue.TryWrite(new InboundMessage(0, "game-types", "system", ServerCommandNames.MigrateIn, snapshotJson));
        targetWorker.ExecuteCycle(testTimestamp);

        var restoredGame = targetScheduler.FindGame("game-types");
        Assert.NotNull(restoredGame);
        var restoredItem = restoredGame.Scope.GetItem("player1");

        Assert.IsType<Vector>(restoredItem.GetProperty("Position"));
        Assert.Equal(new Vector([10, 20]), restoredItem.GetProperty("Position"));

        Assert.IsType<Angle>(restoredItem.GetProperty("Orientation"));
        Assert.Equal(new Angle(5), (Angle)restoredItem.GetProperty("Orientation"));

        Assert.IsType<bool>(restoredItem.GetProperty("IsAlive"));
        Assert.True((bool)restoredItem.GetProperty("IsAlive"));

        Assert.IsType<double>(restoredItem.GetProperty("Health"));
        Assert.Equal(99.5, (double)restoredItem.GetProperty("Health"));

        Assert.IsType<int>(restoredItem.GetProperty("Score"));
        Assert.Equal(1000, (int)restoredItem.GetProperty("Score"));
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
}
