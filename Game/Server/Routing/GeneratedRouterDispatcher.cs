using System.Linq.Expressions;
using GameServer.Messaging;

namespace GameServer.Routing;

public sealed record RouteTarget(int ThreadId, INonBlockingMessageWriter<InboundMessage> Writer);

public sealed class GeneratedRouterDispatcher
{
    private volatile DispatchState _state;

    public GeneratedRouterDispatcher(IReadOnlyList<INonBlockingMessageWriter<InboundMessage>> targetWriters)
    {
        ArgumentNullException.ThrowIfNull(targetWriters);

        _ = targetWriters.Count > 0
            ? 0
            : throw new ArgumentException("At least one target writer is required.", nameof(targetWriters));

        _state = CreateDispatchState(targetWriters);
    }

    public int WriterCount => _state.Targets.Length;

    public RouteTarget Dispatch(int routingKey)
    {
        return _state.CompiledDispatch(routingKey);
    }

    public RouteTarget Dispatch(int routingKey, int overrideThreadId)
    {
        return _state.CompiledRouteDispatch(routingKey, overrideThreadId);
    }

    public void UpdateWriters(IReadOnlyList<INonBlockingMessageWriter<InboundMessage>> targetWriters)
    {
        ArgumentNullException.ThrowIfNull(targetWriters);

        _ = targetWriters.Count > 0
            ? 0
            : throw new ArgumentException("At least one target writer is required.", nameof(targetWriters));

        _state = CreateDispatchState(targetWriters);
    }

    private static DispatchState CreateDispatchState(IReadOnlyList<INonBlockingMessageWriter<InboundMessage>> targetWriters)
    {
        var targets = targetWriters.Select((writer, index) => new RouteTarget(index, writer)).ToArray();

        var compiledDispatch = CompileDispatchDelegate(targets);
        var compiledRouteDispatch = CompileRouteDispatchDelegate(targets);
        return new DispatchState(targets, compiledDispatch, compiledRouteDispatch);
    }

    private static Func<int, RouteTarget> CompileDispatchDelegate(
        RouteTarget[] targetTable)
    {
        var targetTableExpression = Expression.Constant(targetTable, typeof(RouteTarget[]));
        var routingKeyExpression = Expression.Parameter(typeof(int), "routingKey");
        var modulusConstantExpression = Expression.Constant(targetTable.Length, typeof(int));

        var moduloExpression = Expression.Modulo(routingKeyExpression, modulusConstantExpression);
        var offsetExpression = Expression.Add(moduloExpression, modulusConstantExpression);
        var positiveIndexExpression = Expression.Modulo(offsetExpression, modulusConstantExpression);

        var arrayAccessExpression = Expression.ArrayIndex(targetTableExpression, positiveIndexExpression);
        var lambdaExpression = Expression.Lambda<Func<int, RouteTarget>>(arrayAccessExpression, routingKeyExpression);

        return lambdaExpression.Compile();
    }

    private static Func<int, int, RouteTarget> CompileRouteDispatchDelegate(
        RouteTarget[] targetTable)
    {
        var targetTableExpression = Expression.Constant(targetTable, typeof(RouteTarget[]));
        var routingKeyExpression = Expression.Parameter(typeof(int), "routingKey");
        var overrideThreadIdExpression = Expression.Parameter(typeof(int), "overrideThreadId");
        var modulusConstantExpression = Expression.Constant(targetTable.Length, typeof(int));

        var moduloExpression = Expression.Modulo(routingKeyExpression, modulusConstantExpression);
        var offsetExpression = Expression.Add(moduloExpression, modulusConstantExpression);
        var positiveIndexExpression = Expression.Modulo(offsetExpression, modulusConstantExpression);

        var hashTargetAccess = Expression.ArrayIndex(targetTableExpression, positiveIndexExpression);
        var overrideTargetAccess = Expression.ArrayIndex(targetTableExpression, overrideThreadIdExpression);

        var isOverrideValid = Expression.AndAlso(
            Expression.GreaterThanOrEqual(overrideThreadIdExpression, Expression.Constant(0)),
            Expression.LessThan(overrideThreadIdExpression, modulusConstantExpression)
        );

        var resolvedTargetExpression = Expression.Condition(
            isOverrideValid,
            overrideTargetAccess,
            hashTargetAccess
        );

        var lambdaExpression = Expression.Lambda<Func<int, int, RouteTarget>>(
            resolvedTargetExpression,
            routingKeyExpression,
            overrideThreadIdExpression);

        return lambdaExpression.Compile();
    }

    private sealed record DispatchState(
        RouteTarget[] Targets,
        Func<int, RouteTarget> CompiledDispatch,
        Func<int, int, RouteTarget> CompiledRouteDispatch);
}
