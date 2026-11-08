namespace GameServer.Lifecycle;

using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Scaling;

public sealed record ServerShutdownResult(bool CompletedGracefully, int SavedSnapshotCount);

public sealed class ServerShutdownCoordinator : IServerShutdownCoordinator
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);
    private readonly IWorkerPool _workerPool;
    private readonly ISnapshotStore _snapshotStore;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _softStopTimeout;
    private readonly IEndpoint? _endpoint;

    public ServerShutdownCoordinator(
        IWorkerPool workerPool,
        ISnapshotStore snapshotStore,
        TimeProvider timeProvider,
        TimeSpan softStopTimeout)
        : this(workerPool, snapshotStore, timeProvider, softStopTimeout, null)
    {
    }

    public ServerShutdownCoordinator(
        IWorkerPool workerPool,
        ISnapshotStore snapshotStore,
        TimeProvider timeProvider,
        TimeSpan softStopTimeout,
        IEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(workerPool);
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(softStopTimeout, TimeSpan.Zero, nameof(softStopTimeout));

        _workerPool = workerPool;
        _snapshotStore = snapshotStore;
        _timeProvider = timeProvider;
        _softStopTimeout = softStopTimeout;
        _endpoint = endpoint;
    }

    public void SealAcceptance()
    {
        _endpoint?.SealAcceptance();
    }

    public ServerShutdownResult SoftStop()
    {
        _endpoint?.SealAcceptance();
        RequestWorkersSeal();

        var shutdownDeadline = _timeProvider.GetUtcNow() + _softStopTimeout;

        while (_timeProvider.GetUtcNow() < shutdownDeadline && !AreAllWorkersDrained())
        {
            Thread.Sleep(PollInterval);
        }

        var completedGracefully = AreAllWorkersDrained();
        _workerPool.StopAll();

        return new ServerShutdownResult(completedGracefully, _snapshotStore.SavedSnapshotCount);
    }

    public ServerShutdownResult HardStop()
    {
        _endpoint?.SealAcceptance();
        _workerPool.StopAll();

        return new ServerShutdownResult(false, _snapshotStore.SavedSnapshotCount);
    }

    public ServerShutdownResult Restart()
    {
        var stopResult = SoftStop();
        _endpoint?.ResumeAcceptance();
        return stopResult;
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
        return _workerPool
            .CollectStatistics()
            .All(statistics => _workerPool.IsWorkerDrained(statistics.ThreadId));
    }
}
