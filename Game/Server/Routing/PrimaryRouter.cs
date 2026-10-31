namespace GameServer.Routing;

using GameServer.Messaging;

public sealed class PrimaryRouter
{
    private readonly ReshardingTable _reshardingTable;
    private readonly IReadOnlyList<IMessageQueueWriter<InboundMessage>> _writersByThreadId;
    private readonly GeneratedRouterDispatcher _hashDispatcher;

    public PrimaryRouter(
        IReadOnlyList<IMessageQueueWriter<InboundMessage>> writersByThreadId,
        ReshardingTable reshardingTable)
    {
        ArgumentNullException.ThrowIfNull(writersByThreadId);
        ArgumentNullException.ThrowIfNull(reshardingTable);

        if (writersByThreadId.Count == 0)
        {
            throw new ArgumentException("At least one worker writer is required.", nameof(writersByThreadId));
        }

        _writersByThreadId = writersByThreadId;
        _reshardingTable = reshardingTable;
        _hashDispatcher = new GeneratedRouterDispatcher(writersByThreadId);
    }

    public RouteTarget ResolveTarget(int routingKey, string gameId)
    {
        var overriddenThreadId = _reshardingTable.ResolveRoute(gameId);

        return overriddenThreadId is null
            ? _hashDispatcher.Dispatch(routingKey)
            : new RouteTarget(overriddenThreadId.Value, _writersByThreadId[overriddenThreadId.Value]);
    }
}
