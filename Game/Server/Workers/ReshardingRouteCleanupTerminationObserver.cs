namespace GameServer.Workers;

using GameServer.Resharding;

public sealed class ReshardingRouteCleanupTerminationObserver : IGameTerminationObserver
{
    private readonly IReshardingTable _reshardingTable;
    private readonly IGameTerminationObserver? _innerObserver;

    public ReshardingRouteCleanupTerminationObserver(
        IReshardingTable reshardingTable,
        IGameTerminationObserver? innerObserver = null)
    {
        ArgumentNullException.ThrowIfNull(reshardingTable);

        _reshardingTable = reshardingTable;
        _innerObserver = innerObserver;
    }

    public void NotifyGameTerminated(string gameId, GameTerminationReason terminationReason)
    {
        _ = terminationReason != GameTerminationReason.GameTransferred && RemoveRoute(gameId);
        _innerObserver?.NotifyGameTerminated(gameId, terminationReason);
    }

    private bool RemoveRoute(string gameId)
    {
        _reshardingTable.RemoveRoute(gameId);
        return true;
    }
}
