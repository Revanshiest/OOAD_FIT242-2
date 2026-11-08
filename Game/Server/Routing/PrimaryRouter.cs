using System.Linq.Expressions;
using Game.IoC;
using GameServer.Messaging;
using GameServer.Resharding;

namespace GameServer.Routing;

public sealed class PrimaryRouter : IRouter
{
    private static readonly Action<IReshardingTable, string, int> RouteCacher = CompileRouteCacher();

    private readonly IReshardingTable _reshardingTable;
    private readonly List<IMessageQueueWriter<InboundMessage>> _writersByThreadId;
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

        _writersByThreadId = writersByThreadId.ToList();
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
        ArgumentNullException.ThrowIfNull(writer);

        lock (_syncLock)
        {
            while (_writersByThreadId.Count <= threadId)
            {
                _writersByThreadId.Add(null!);
            }

            _writersByThreadId[threadId] = writer;

            var activeWriters = _writersByThreadId.Where(writer => writer is not null).ToList();
            _hashDispatcher.UpdateWriters(activeWriters);
            _reshardingTable.UpdateThreadCount(_writersByThreadId.Count);
        }
    }

    public void UnregisterWorker(int threadId)
    {
        lock (_syncLock)
        {
            var isValidThread = threadId >= 0 && threadId < _writersByThreadId.Count;
            while (isValidThread)
            {
                _writersByThreadId[threadId] = null!;

                var activeWriters = _writersByThreadId.Where(writer => writer is not null).ToList();
                _ = activeWriters.Count > 0 && UpdateActiveWriters(activeWriters);
                _reshardingTable.RemoveRoutesForThread(threadId);
                break;
            }
        }
    }

    private bool UpdateActiveWriters(IReadOnlyList<IMessageQueueWriter<InboundMessage>> activeWriters)
    {
        _hashDispatcher.UpdateWriters(activeWriters);
        return true;
    }

    public RouteTarget ResolveTarget(int routingKey, string gameId)
    {
        var overrideId = _reshardingTable.ResolveRoute(gameId) ?? -1;
        var target = _hashDispatcher.Dispatch(routingKey, overrideId);
        RouteCacher(_reshardingTable, gameId, target.ThreadId);
        return target;
    }

    private static Action<IReshardingTable, string, int> CompileRouteCacher()
    {
        var tableParam = Expression.Parameter(typeof(IReshardingTable), "table");
        var gameIdParam = Expression.Parameter(typeof(string), "gameId");
        var threadIdParam = Expression.Parameter(typeof(int), "threadId");

        var isNullOrWhiteSpaceMethod = typeof(string).GetMethod(
            nameof(string.IsNullOrWhiteSpace),
            [typeof(string)])!;
        var isInvalidId = Expression.Call(isNullOrWhiteSpaceMethod, gameIdParam);

        var setRouteMethod = typeof(IReshardingTable).GetMethod(
            nameof(IReshardingTable.SetRoute),
            [typeof(string), typeof(int)])!;
        var callSetRoute = Expression.Call(tableParam, setRouteMethod, gameIdParam, threadIdParam);

        var body = Expression.IfThen(Expression.Not(isInvalidId), callSetRoute);
        return Expression.Lambda<Action<IReshardingTable, string, int>>(body, tableParam, gameIdParam, threadIdParam).Compile();
    }
}
