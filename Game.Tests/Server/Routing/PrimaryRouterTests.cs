using GameServer.Routing;
using GameServer.Resharding;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Routing;

public class PrimaryRouterTests
{
    private readonly List<RecordingMessageWriter> _recordingWriters =
    [
        new RecordingMessageWriter(),
        new RecordingMessageWriter()
    ];

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenWriterListIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new PrimaryRouter([], new ReshardingTable(1)));
    }

    [Fact]
    public void ResolveTarget_UsesHashDispatch_WhenNoReshardingOverrideIsRegistered()
    {
        var primaryRouter = CreateRouter();

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(1, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[1], routeTarget.Writer);
    }

    [Fact]
    public void ResolveTarget_PrefersReshardingOverride_OverHashDispatch()
    {
        var reshardingTable = new ReshardingTable(2);
        var primaryRouter = new PrimaryRouter(_recordingWriters.ToList(), reshardingTable);
        reshardingTable.SetRoute("game-1", 0);

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(0, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[0], routeTarget.Writer);
    }

    [Fact]
    public void ResolveTarget_ReturnsToHashDispatch_AfterOverrideRemoval()
    {
        var reshardingTable = new ReshardingTable(2);
        var primaryRouter = new PrimaryRouter(_recordingWriters.ToList(), reshardingTable);
        reshardingTable.SetRoute("game-1", 0);
        reshardingTable.RemoveRoute("game-1");

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(1, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[1], routeTarget.Writer);
    }

    private PrimaryRouter CreateRouter()
    {
        return new PrimaryRouter(_recordingWriters.ToList(), new ReshardingTable(2));
    }
}
