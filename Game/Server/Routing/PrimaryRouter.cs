using System.Linq.Expressions;
using Game.IoC;
using GameServer.Messaging;
using GameServer.Resharding;

namespace GameServer.Routing;

public sealed class PrimaryRouter : IRouter
{
    private static readonly Func<IReshardingTable, string, int, int> RouteCacher = CompileRouteCacher();

    private readonly IReshardingTable _reshardingTable;
    private readonly List<IMessageQueueWriter<InboundMessage>?> _writersByThreadId;
    private readonly GeneratedRouterDispatcher _hashDispatcher;
    private readonly object _syncLock = new();

    public PrimaryRouter(
        IReadOnlyList<IMessageQueueWriter<InboundMessage>> writersByThreadId,
        IReshardingTable reshardingTable)
    {
        ArgumentNullException.ThrowIfNull(writersByThreadId);
        ArgumentNullException.ThrowIfNull(reshardingTable);

        _ = writersByThreadId.Count > 0
            ? 0
            : throw new ArgumentException("At least one worker writer is required.", nameof(writersByThreadId));

        _writersByThreadId = writersByThreadId.Cast<IMessageQueueWriter<InboundMessage>?>().ToList();
        _reshardingTable = reshardingTable;
        _hashDispatcher = new GeneratedRouterDispatcher(writersByThreadId);
    }

    public static PrimaryRouter Create(IReadOnlyList<IMessageQueueWriter<InboundMessage>> writersByThreadId, int threadCount)
    {
        try
        {
            return Ioc.Resolve<PrimaryRouter>(GameServer.IoC.RegisterIoCDependencyServerRouter.CreateRouterKey, writersByThreadId, threadCount);
        }
        catch
        {
            return new PrimaryRouter(writersByThreadId, new ReshardingTable(threadCount));
        }
    }

    public IReshardingTable ReshardingTable => _reshardingTable;

    public int WorkerCount => _hashDispatcher.WriterCount;

    public void RegisterWorker(int threadId, IMessageQueueWriter<InboundMessage> writer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threadId);
        ArgumentNullException.ThrowIfNull(writer);

        lock (_syncLock)
        {
            while (_writersByThreadId.Count <= threadId)
            {
                _writersByThreadId.Add(null);
            }

            _writersByThreadId[threadId] = writer;

            _reshardingTable.UpdateThreadCount(_writersByThreadId.Count);
            UpdateActiveWriters();
        }
    }

    public void UnregisterWorker(int threadId)
    {
        lock (_syncLock)
        {
            var isValidThread = threadId >= 0 && threadId < _writersByThreadId.Count;
            while (isValidThread)
            {
                _writersByThreadId[threadId] = null;

                UpdateActiveWriters();
                _reshardingTable.RemoveRoutesForThread(threadId);
                break;
            }
        }
    }

    private void UpdateActiveWriters()
    {
        var activeTargets = _writersByThreadId
            .Select((writer, threadId) => (writer, threadId))
            .Where(entry => entry.writer is not null)
            .Select(entry => new RouteTarget(entry.threadId, entry.writer!))
            .ToArray();
        _hashDispatcher.UpdateTargets(activeTargets);
    }

    public RouteTarget ResolveTarget(int routingKey, string gameId)
    {
        lock (_syncLock)
        {
            var overrideId = _reshardingTable.ResolveRoute(gameId) ?? -1;
            var target = _hashDispatcher.Dispatch(routingKey, overrideId);
            var chosenThreadId = RouteCacher(_reshardingTable, gameId, target.ThreadId);
            return _hashDispatcher.Dispatch(routingKey, chosenThreadId);
        }
    }

    private static Func<IReshardingTable, string, int, int> CompileRouteCacher()
    {
        var tableParam = Expression.Parameter(typeof(IReshardingTable), "table");
        var gameIdParam = Expression.Parameter(typeof(string), "gameId");
        var threadIdParam = Expression.Parameter(typeof(int), "threadId");

        var isNullOrWhiteSpaceMethod = typeof(string).GetMethod(
            nameof(string.IsNullOrWhiteSpace),
            [typeof(string)])!;
        var isInvalidId = Expression.Call(isNullOrWhiteSpaceMethod, gameIdParam);

        var setRouteMethod = typeof(IReshardingTable).GetMethod(
            nameof(IReshardingTable.GetOrAddRoute),
            [typeof(string), typeof(int)])!;
        var callSetRoute = Expression.Call(tableParam, setRouteMethod, gameIdParam, threadIdParam);

        var body = Expression.Condition(isInvalidId, threadIdParam, callSetRoute);
        return Expression.Lambda<Func<IReshardingTable, string, int, int>>(body, tableParam, gameIdParam, threadIdParam).Compile();
    }
}
