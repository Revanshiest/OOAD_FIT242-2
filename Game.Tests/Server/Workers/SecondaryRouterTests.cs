namespace Game.Tests.Server.Workers;

using Game.Commands;
using Game.Tests.Server.Messaging;
using Game.Tests.Server.Resharding;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;

public class SecondaryRouterTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SecondaryRouter_EncapsulatesGameLookupAndRouting()
    {
        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>()),
            new RecordingOverflowSink(),
            new InMemorySnapshotStore());

        var secondaryRouter = new SecondaryRouter(
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)),
            new DefaultGameFactory(),
            GameWorkerSettings.CreateDefault(),
            supportServices);

        var msg = new InboundMessage(0, "game-sec", "token", ServerCommandNames.CreateItem,
            """{"itemId":"item1","type":"Ship","position":[1,2],"speed":[3,4]}""");

        var routed = secondaryRouter.RouteMessage(msg, isWorkerSealedByTick: false, new Dictionary<string, int>());
        Assert.True(routed);

        var recipient = secondaryRouter.FindRecipient("game-sec");
        Assert.NotNull(recipient);
        Assert.IsType<GameServer.Games.Game>(recipient);
        Assert.Single(secondaryRouter.Recipients);

        secondaryRouter.RemoveRecipient("game-sec");
        Assert.Null(secondaryRouter.FindRecipient("game-sec"));
        Assert.Empty(secondaryRouter.Recipients);
    }

    [Fact]
    public void SecondaryRouter_PreventsInstantResurrection_OfEvictedSpammer()
    {
        var scheduler = new PriorityRoundRobinScheduler(
            GameWorkerSettings.CreateDefault(),
            new CommandRateLimiter(16, 64),
            NullGameTerminationObserver.Instance);
        var supportServices = new GameWorkerSupportServices(
            new JsonServerSnapshotCodec(),
            new TestTransitResolver(new Dictionary<int, IMessageQueueWriter<InboundMessage>>()),
            new RecordingOverflowSink(),
            new InMemorySnapshotStore());

        var settings = new GameWorkerSettings(
            commandQuantumPerGameActivation: 1,
            maxQueueLengthPerGame: 2,
            maxActiveGames: 10,
            maxExternalAwaitDuration: TimeSpan.FromMinutes(1),
            rateLimitBucketCapacity: 10,
            rateLimitTokensPerSecond: 10);

        var router = new SecondaryRouter(
            scheduler,
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)),
            new DefaultGameFactory(),
            settings,
            supportServices);

        router.RouteMessage(new InboundMessage(0, "spammer-1", "token", ServerCommandNames.Resume, "{}"), false, new Dictionary<string, int>());
        router.RouteMessage(new InboundMessage(0, "spammer-1", "token", ServerCommandNames.Resume, "{}"), false, new Dictionary<string, int>());

        var accepted3 = router.RouteMessage(new InboundMessage(0, "spammer-1", "token", ServerCommandNames.Resume, "{}"), false, new Dictionary<string, int>());
        Assert.False(accepted3);
        Assert.Null(router.FindRecipient("spammer-1"));

        var acceptedNext = router.RouteMessage(new InboundMessage(0, "spammer-1", "token", ServerCommandNames.Resume, "{}"), false, new Dictionary<string, int>());
        Assert.False(acceptedNext);
        Assert.Null(router.FindRecipient("spammer-1"));
        Assert.Equal(0, scheduler.RegisteredGameCount);
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

