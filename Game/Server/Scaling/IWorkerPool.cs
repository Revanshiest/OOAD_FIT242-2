namespace GameServer.Scaling;

using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;

public interface IWorkerPool : ITransitResolver, IWorkerMessagePoster
{
    int WorkerCount { get; }

    int StartWorker();

    void StopWorker(int threadId);

    void StopAll();

    bool IsWorkerDrained(int threadId);

    IScope? TryGetGameScope(int threadId, string gameId);

    IReadOnlyList<WorkerStatistics> CollectStatistics();

    void AttachRouter(PrimaryRouter router);

    void ScaleDownWorker(int sourceThreadId, int targetThreadId, IGameMigrationCoordinator coordinator);
}

