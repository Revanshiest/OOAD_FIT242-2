using GameServer.Resharding;

namespace Game.Tests.Server.Routing;

public class ReshardingTableTests
{
    [Fact]
    public void SetRoute_And_ResolveRoute_ReturnRegisteredThread()
    {
        var reshardingTable = new ReshardingTable(2);

        reshardingTable.SetRoute("game-1", 1);

        Assert.Equal(1, reshardingTable.ResolveRoute("game-1"));
    }

    [Fact]
    public void ResolveRoute_ReturnsNull_WhenRouteIsNotRegistered()
    {
        var reshardingTable = new ReshardingTable(2);

        Assert.Null(reshardingTable.ResolveRoute("game-missing"));
    }

    [Fact]
    public void RemoveRoute_ReturnsResolutionToUnroutedState()
    {
        var reshardingTable = new ReshardingTable(2);
        reshardingTable.SetRoute("game-1", 0);

        reshardingTable.RemoveRoute("game-1");

        Assert.Null(reshardingTable.ResolveRoute("game-1"));
    }

    [Fact]
    public void SetRoute_OverwritesPreviousRoute()
    {
        var reshardingTable = new ReshardingTable(2);
        reshardingTable.SetRoute("game-1", 0);

        reshardingTable.SetRoute("game-1", 1);

        Assert.Equal(1, reshardingTable.ResolveRoute("game-1"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void SetRoute_ThrowsArgumentOutOfRangeException_WhenThreadIdIsOutsideTableRange(int threadId)
    {
        var reshardingTable = new ReshardingTable(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => reshardingTable.SetRoute("game-1", threadId));
    }

    [Fact]
    public void Snapshot_ReturnsAllRegisteredRoutes()
    {
        var reshardingTable = new ReshardingTable(3);
        reshardingTable.SetRoute("game-1", 0);
        reshardingTable.SetRoute("game-2", 2);

        var snapshot = reshardingTable.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(0, snapshot["game-1"]);
        Assert.Equal(2, snapshot["game-2"]);
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenThreadCountIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReshardingTable(0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetRoute_ThrowsArgumentException_WhenGameIdIsInvalid(string? gameId)
    {
        var reshardingTable = new ReshardingTable(2);

        Assert.Throws<ArgumentException>(() => reshardingTable.SetRoute(gameId!, 0));
    }
}
