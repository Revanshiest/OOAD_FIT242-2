using GameServer.Messaging;
using GameServer.Routing;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Routing;

public class GeneratedRouterDispatcherTests
{
    private const int WriterCount = 3;

    private readonly List<RecordingMessageWriter> _recordingWriters = Enumerable
        .Range(0, WriterCount)
        .Select(_ => new RecordingMessageWriter())
        .ToList();

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenWriterTableIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new GeneratedRouterDispatcher([]));
    }

    [Fact]
    public void Dispatch_RoutesEachKeyToWriterAtModuloIndex()
    {
        var dispatcher = new GeneratedRouterDispatcher(_recordingWriters.ToList());

        var expectedWriterIndices = new[] { 0, 1, 2, 0, 1, 2 };
        var resolvedTargets = expectedWriterIndices
            .Select((_, routingKey) => dispatcher.Dispatch(routingKey))
            .ToList();

        Assert.All(resolvedTargets, target => Assert.InRange(target.ThreadId, 0, WriterCount - 1));
        Assert.Equal(0, resolvedTargets[0].ThreadId);
        Assert.Equal(1, resolvedTargets[1].ThreadId);
        Assert.Equal(2, resolvedTargets[2].ThreadId);
        Assert.Equal(0, resolvedTargets[3].ThreadId);
        Assert.Equal(1, resolvedTargets[4].ThreadId);
        Assert.Equal(2, resolvedTargets[5].ThreadId);
    }

    [Fact]
    public void Dispatch_DeliversMessageToResolvedWriter()
    {
        var dispatcher = new GeneratedRouterDispatcher(_recordingWriters.ToList());
        var message = new InboundMessage(2, "game-1", "token", "Move", "{}");

        var routeTarget = dispatcher.Dispatch(2);

        routeTarget.Writer.TryWrite(message);
        Assert.Single(_recordingWriters[2].WrittenMessages);
        Assert.Empty(_recordingWriters[0].WrittenMessages);
        Assert.Empty(_recordingWriters[1].WrittenMessages);
    }

    [Fact]
    public void Dispatch_MapsNegativeKeyIntoTableRange_WithoutBranching()
    {
        var dispatcher = new GeneratedRouterDispatcher(_recordingWriters.ToList());

        var routeTarget = dispatcher.Dispatch(-1);

        Assert.InRange(routeTarget.ThreadId, 0, WriterCount - 1);
    }

    [Fact]
    public void Dispatch_MapsExtremeIntegerKeysIntoTableRange()
    {
        var dispatcher = new GeneratedRouterDispatcher(_recordingWriters.ToList());

        var maximumKeyTarget = dispatcher.Dispatch(int.MaxValue);
        var minimumKeyTarget = dispatcher.Dispatch(int.MinValue);

        Assert.InRange(maximumKeyTarget.ThreadId, 0, WriterCount - 1);
        Assert.InRange(minimumKeyTarget.ThreadId, 0, WriterCount - 1);
    }

    [Fact]
    public void Dispatch_DistributesUniformKeysAcrossAllWriters()
    {
        var dispatcher = new GeneratedRouterDispatcher(_recordingWriters.ToList());
        const int routingKeyCount = 300;

        Enumerable
            .Range(0, routingKeyCount)
            .ToList()
            .ForEach(routingKey => dispatcher.Dispatch(routingKey).Writer.TryWrite(CreateMessage(routingKey)));

        Assert.All(_recordingWriters, writer => Assert.Equal(routingKeyCount / WriterCount, writer.WrittenMessages.Count));
    }

    private static InboundMessage CreateMessage(int routingKey)
    {
        return new InboundMessage(routingKey, $"game-{routingKey}", "token", "Move", "{}");
    }
}
