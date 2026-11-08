namespace GameServer.Scaling;

using System.Collections.Concurrent;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Workers;

public sealed record WorkerStatistics(
    int ThreadId,
    int GameCount,
    int PendingInputMessages,
    int TotalQueuedCommandCount);

public sealed class WorkerPool : IWorkerPool
{
    public const int DefaultInputQueueCapacity = 65536;

    private readonly ConcurrentDictionary<int, WorkerCell> _cellsByThreadId = new();
    private readonly List<PrimaryRouter> _attachedRouters = new();
    private readonly object _routerLock = new();
    private readonly GameWorkerSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IGameTerminationObserver _terminationObserver;
    private readonly IServerCommandDeserializer _commandDeserializer;
    private readonly IGameFactory _gameFactory;
    private readonly ISnapshotCodec _snapshotCodec;
    private readonly ITransitOverflowSink _overflowSink;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IWorkerHostFactory _workerHostFactory;
    private int _nextThreadId;

    public WorkerPool(
        GameWorkerSettings settings,
        TimeProvider timeProvider,
        IGameTerminationObserver terminationObserver,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        ISnapshotCodec snapshotCodec,
        ITransitOverflowSink overflowSink,
        ISnapshotStore snapshotStore)
        : this(
            settings,
            timeProvider,
            terminationObserver,
            commandDeserializer,
            gameFactory,
            snapshotCodec,
            overflowSink,
            snapshotStore,
            new DefaultWorkerHostFactory())
    {
    }

    public WorkerPool(
        GameWorkerSettings settings,
        TimeProvider timeProvider,
        IGameTerminationObserver terminationObserver,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        ISnapshotCodec snapshotCodec,
        ITransitOverflowSink overflowSink,
        ISnapshotStore snapshotStore,
        IWorkerHostFactory workerHostFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(terminationObserver);
        ArgumentNullException.ThrowIfNull(commandDeserializer);
        ArgumentNullException.ThrowIfNull(gameFactory);
        ArgumentNullException.ThrowIfNull(snapshotCodec);
        ArgumentNullException.ThrowIfNull(overflowSink);
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(workerHostFactory);

        _settings = settings;
        _timeProvider = timeProvider;
        _terminationObserver = terminationObserver;
        _commandDeserializer = commandDeserializer;
        _gameFactory = gameFactory;
        _snapshotCodec = snapshotCodec;
        _overflowSink = overflowSink;
        _snapshotStore = snapshotStore;
        _workerHostFactory = workerHostFactory;
    }

    public int WorkerCount => _cellsByThreadId.Count;

    public int StartWorker()
    {
        var threadId = Interlocked.Increment(ref _nextThreadId) - 1;
        var cell = _workerHostFactory.CreateWorkerCell(
            threadId,
            _settings,
            _timeProvider,
            _terminationObserver,
            _commandDeserializer,
            _gameFactory,
            _snapshotCodec,
            this,
            _overflowSink,
            _snapshotStore);

        cell.WorkerHost.Start();
        (_overflowSink as BackupTransitThread)?.Start();

        _cellsByThreadId[threadId] = cell;

        lock (_routerLock)
        {
            foreach (var router in _attachedRouters)
            {
                router.RegisterWorker(threadId, cell.InputQueue);
            }
        }

        return threadId;
    }

    public void AttachRouter(PrimaryRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);

        lock (_routerLock)
        {
            _attachedRouters.Add(router);
            foreach (var (threadId, cell) in _cellsByThreadId)
            {
                router.RegisterWorker(threadId, cell.InputQueue);
            }
        }
    }

    public void ScaleDownWorker(int sourceThreadId, int targetThreadId, IGameMigrationCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        var sourceCell = _cellsByThreadId.GetValueOrDefault(sourceThreadId)
            ?? throw new KeyNotFoundException($"Worker '{sourceThreadId}' is not running in the pool.");

        _ = _cellsByThreadId.ContainsKey(targetThreadId)
            ? 0
            : throw new KeyNotFoundException($"Target worker '{targetThreadId}' is not running in the pool.");

        foreach (var gameId in sourceCell.Scheduler.RegisteredGameIds)
        {
            coordinator.BeginThreadMigration(new GameMigrationRequest(gameId, sourceThreadId, targetThreadId));
        }
    }

    public void StopWorker(int threadId)
    {
        var hasCell = _cellsByThreadId.TryRemove(threadId, out var removedCell);
        _ = hasCell
            ? 0
            : throw new KeyNotFoundException($"Worker '{threadId}' is not running in the pool.");

        lock (_routerLock)
        {
            foreach (var router in _attachedRouters)
            {
                router.UnregisterWorker(threadId);
            }
        }

        removedCell!.WorkerHost.Stop();
        removedCell.WorkerHost.Join();
    }

    public void StopAll()
    {
        var threadIds = _cellsByThreadId.Keys.ToList();
        foreach (var threadId in threadIds)
        {
            StopWorker(threadId);
        }

        (_overflowSink as BackupTransitThread)?.Stop();
    }

    public bool TryPost(int threadId, InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var targetCell = _cellsByThreadId.GetValueOrDefault(threadId);
        return targetCell is not null && targetCell.InputQueue.TryWrite(message);
    }

    public IScope? TryGetGameScope(int threadId, string gameId)
    {
        return _cellsByThreadId.TryGetValue(threadId, out var cell)
            ? cell.Scheduler.FindGame(gameId)?.Scope
            : null;
    }

    public IReadOnlyList<WorkerStatistics> CollectStatistics()
    {
        return _cellsByThreadId.Values
            .Select(cell => new WorkerStatistics(
                cell.WorkerHost.ThreadId,
                cell.Scheduler.RegisteredGameCount,
                cell.InputQueue.PendingCount,
                cell.Scheduler.TotalQueuedCommandCount))
            .ToList();
    }

    public bool IsWorkerDrained(int threadId)
    {
        var targetCell = _cellsByThreadId.GetValueOrDefault(threadId);
        return targetCell is not null
            && targetCell.InputQueue.PendingCount == 0
            && targetCell.WorkerHost.IsShutdownCompleted;
    }

    public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId)
    {
        var targetCell = _cellsByThreadId.GetValueOrDefault(threadId);
        return targetCell?.InputQueue
            ?? throw new KeyNotFoundException($"Worker '{threadId}' is not present in the worker pool.");
    }
}
