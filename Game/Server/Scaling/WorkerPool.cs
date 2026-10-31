using System.Collections.Concurrent;

namespace GameServer.Scaling;

using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;

public sealed record WorkerStatistics(
    int ThreadId,
    int GameCount,
    int PendingInputMessages,
    int TotalQueuedCommandCount);

public sealed class WorkerPool : ITransitResolver, IWorkerMessagePoster
{
    public const int DefaultInputQueueCapacity = 4096;
    private readonly GameWorkerSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IGameTerminationObserver _terminationObserver;
    private readonly IServerCommandDeserializer _commandDeserializer;
    private readonly IGameFactory _gameFactory;
    private readonly ISnapshotCodec _snapshotCodec;
    private readonly ITransitOverflowSink _overflowSink;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ConcurrentDictionary<int, WorkerCell> _cellsByThreadId = new();
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
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _terminationObserver = terminationObserver ?? throw new ArgumentNullException(nameof(terminationObserver));
        _commandDeserializer = commandDeserializer ?? throw new ArgumentNullException(nameof(commandDeserializer));
        _gameFactory = gameFactory ?? throw new ArgumentNullException(nameof(gameFactory));
        _snapshotCodec = snapshotCodec ?? throw new ArgumentNullException(nameof(snapshotCodec));
        _overflowSink = overflowSink ?? throw new ArgumentNullException(nameof(overflowSink));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    }

    public int WorkerCount => _cellsByThreadId.Count;

    public int StartWorker()
    {
        var threadId = Interlocked.Increment(ref _nextThreadId) - 1;
        var inputQueue = new BoundedMessageQueue<InboundMessage>(DefaultInputQueueCapacity);
        var scheduler = new PriorityRoundRobinScheduler(
            _settings,
            new CommandRateLimiter(_settings.RateLimitBucketCapacity, _settings.RateLimitTokensPerSecond),
            _terminationObserver);
        var workerSupportServices = new GameWorkerSupportServices(
            _snapshotCodec,
            this,
            _overflowSink,
            _snapshotStore);
        var worker = new GameWorker(
            inputQueue,
            scheduler,
            _commandDeserializer,
            _gameFactory,
            _settings,
            workerSupportServices);
        var workerHost = new WorkerThreadHost(threadId, worker, inputQueue, _timeProvider);

        workerHost.Start();
        _cellsByThreadId[threadId] = new WorkerCell(workerHost, scheduler, inputQueue);

        return threadId;
    }

    public void StopWorker(int threadId)
    {
        if (!_cellsByThreadId.TryRemove(threadId, out var removedCell))
        {
            throw new KeyNotFoundException($"Worker '{threadId}' is not running in the pool.");
        }

        removedCell.WorkerHost.Stop();
        removedCell.WorkerHost.Join();
    }

    public bool TryPost(int threadId, InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _cellsByThreadId.TryGetValue(threadId, out var targetCell) && targetCell.InputQueue.TryWrite(message);
    }

    public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId)
    {
        return _cellsByThreadId.TryGetValue(threadId, out var targetCell)
            ? targetCell.InputQueue
            : throw new KeyNotFoundException($"Worker '{threadId}' is not running in the pool.");
    }

    public bool IsWorkerDrained(int threadId)
    {
        return _cellsByThreadId.TryGetValue(threadId, out var targetCell) && targetCell.WorkerHost.IsShutdownCompleted;
    }

    public IScope? TryGetGameScope(int threadId, string gameId)
    {
        return _cellsByThreadId.TryGetValue(threadId, out var targetCell)
            ? targetCell.Scheduler.FindGame(gameId)?.Scope
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
            .OrderBy(statistics => statistics.ThreadId)
            .ToList();
    }

    public void StopAll()
    {
        _cellsByThreadId.Keys.ToList().ForEach(StopWorker);
    }

    private sealed record WorkerCell(
        WorkerThreadHost WorkerHost,
        PriorityRoundRobinScheduler Scheduler,
        BoundedMessageQueue<InboundMessage> InputQueue);
}
