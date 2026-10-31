using System.Linq.Expressions;

namespace GameServer.Routing;

using GameServer.Messaging;

public sealed record RouteTarget(int ThreadId, IMessageQueueWriter<InboundMessage> Writer);

public sealed class GeneratedRouterDispatcher
{
    private readonly IMessageQueueWriter<InboundMessage>[] _writerTable;
    private readonly Func<int, IMessageQueueWriter<InboundMessage>> _compiledDispatch;

    public GeneratedRouterDispatcher(IReadOnlyList<IMessageQueueWriter<InboundMessage>> targetWriters)
    {
        ArgumentNullException.ThrowIfNull(targetWriters);

        if (targetWriters.Count == 0)
        {
            throw new ArgumentException("At least one target writer is required.", nameof(targetWriters));
        }

        _writerTable = targetWriters.ToArray();
        _compiledDispatch = CompileDispatchDelegate(_writerTable);
    }

    public RouteTarget Dispatch(int routingKey)
    {
        var threadId = unchecked((int)((uint)routingKey % (uint)_writerTable.Length));

        return new RouteTarget(threadId, _compiledDispatch(routingKey));
    }

    private static Func<int, IMessageQueueWriter<InboundMessage>> CompileDispatchDelegate(
        IMessageQueueWriter<InboundMessage>[] writerTable)
    {
        var writerTableExpression = Expression.Constant(writerTable, typeof(IMessageQueueWriter<InboundMessage>[]));
        var routingKeyExpression = Expression.Parameter(typeof(int), "routingKey");
        var normalizedKeyExpression = Expression.Modulo(
            Expression.Convert(routingKeyExpression, typeof(uint)),
            Expression.Constant((uint)writerTable.Length));
        var writerExpression = Expression.ArrayIndex(
            writerTableExpression,
            Expression.Convert(normalizedKeyExpression, typeof(int)));

        return Expression
            .Lambda<Func<int, IMessageQueueWriter<InboundMessage>>>(writerExpression, routingKeyExpression)
            .Compile();
    }
}
