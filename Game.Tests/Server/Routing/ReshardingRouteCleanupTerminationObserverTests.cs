namespace Game.Tests.Server.Routing;

using GameServer.Resharding;
using GameServer.Workers;

public class ReshardingRouteCleanupTerminationObserverTests
{
    [Fact]
    public void ReshardingRouteCleanupTerminationObserver_RemovesRouteOnGameTermination()
    {
        var reshardingTable = new ReshardingTable(2);
        reshardingTable.SetRoute("game-term", 1);
        Assert.Equal(1, reshardingTable.ResolveRoute("game-term"));

        var observer = new ReshardingRouteCleanupTerminationObserver(reshardingTable);

        observer.NotifyGameTerminated("game-term", GameTerminationReason.GameTransferred);
        Assert.Equal(1, reshardingTable.ResolveRoute("game-term"));

        observer.NotifyGameTerminated("game-term", GameTerminationReason.FloodQueueOverflow);
        Assert.Null(reshardingTable.ResolveRoute("game-term"));
    }
}
