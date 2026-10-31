namespace GameServer.Lifecycle;

using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Scaling;

public sealed record ServerShutdownResult(bool CompletedGracefully, int SavedSnapshotCount);

public sealed class ServerShutdownCoordinator
{
    private readonly WorkerPool _workerPool;
    private readonly ISnapshotStore _snapshotStore;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _softStopTimeout;

    public ServerShutdownCoordinator(
        WorkerPool workerPool,
        ISnapshotStore snapshotStore,
        TimeProvider timeProvider,
        TimeSpan softStopTimeout)
    {
        _workerPool = workerPool ?? throw new ArgumentNullException(nameof(workerPool));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        if (softStopTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(softStopTimeout), softStopTimeout, "Soft stop timeout must be positive.");
        }

        _softStopTimeout = softStopTimeout;
    }

    public ServerShutdownResult SoftStop()
    {
        RequestWorkersSeal();

        var shutdownDeadline = _timeProvider.GetUtcNow() + _softStopTimeout;

        while (_timeProvider.GetUtcNow() < shutdownDeadline)
        {
            if (AreAllWorkersDrained())
            {
                _workerPool.StopAll();

                return new ServerShutdownResult(true, _snapshotStore.SavedSnapshotCount);
            }

            Thread.Sleep(PollInterval);
        }

        return HardStop();
    }

    public ServerShutdownResult HardStop()
    {
        _workerPool.StopAll();

        return new ServerShutdownResult(false, _snapshotStore.SavedSnapshotCount);
    }

    private void RequestWorkersSeal()
    {
        _workerPool
            .CollectStatistics()
            .ToList()
            .ForEach(statistics => _workerPool.TryPost(
                statistics.ThreadId,
                new InboundMessage(statistics.ThreadId, string.Empty, "system", ServerCommandNames.TickMarker, "{}")));
    }

    private bool AreAllWorkersDrained()
    {
        var workerStatistics = _workerPool.CollectStatistics();

        return workerStatistics.Count > 0
            && workerStatistics.All(statistics => _workerPool.IsWorkerDrained(statistics.ThreadId));
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
}
