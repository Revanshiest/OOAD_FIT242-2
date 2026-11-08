namespace GameServer.Scaling;

using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Workers;

public sealed record WorkerCell(
    WorkerThreadHost WorkerHost,
    PriorityRoundRobinScheduler Scheduler,
    BoundedMessageQueue<InboundMessage> InputQueue);

public interface IWorkerHostFactory
{
    WorkerCell CreateWorkerCell(
        int threadId,
        GameWorkerSettings settings,
        TimeProvider timeProvider,
        IGameTerminationObserver terminationObserver,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        ISnapshotCodec snapshotCodec,
        ITransitResolver transitResolver,
        ITransitOverflowSink overflowSink,
        ISnapshotStore snapshotStore);
}

public sealed class DefaultWorkerHostFactory : IWorkerHostFactory
{
    public WorkerCell CreateWorkerCell(
        int threadId,
        GameWorkerSettings settings,
        TimeProvider timeProvider,
        IGameTerminationObserver terminationObserver,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        ISnapshotCodec snapshotCodec,
        ITransitResolver transitResolver,
        ITransitOverflowSink overflowSink,
        ISnapshotStore snapshotStore)
    {
        var inputQueue = new BoundedMessageQueue<InboundMessage>(WorkerPool.DefaultInputQueueCapacity);
        var scheduler = new PriorityRoundRobinScheduler(
            settings,
            new CommandRateLimiter(settings.RateLimitBucketCapacity, settings.RateLimitTokensPerSecond),
            terminationObserver);
        var workerSupportServices = new GameWorkerSupportServices(
            snapshotCodec,
            transitResolver,
            overflowSink,
            snapshotStore);
        var worker = new GameWorker(
            inputQueue,
            scheduler,
            commandDeserializer,
            gameFactory,
            settings,
            workerSupportServices);
        var workerHost = new WorkerThreadHost(threadId, worker, inputQueue, timeProvider);

        return new WorkerCell(workerHost, scheduler, inputQueue);
    }
}

