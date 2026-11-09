namespace GameServer.Resharding;

public interface IReshardingTable
{
    int ThreadCount { get; }

    void UpdateThreadCount(int threadCount);

    void SetRoute(string gameId, int threadId);

    int GetOrAddRoute(string gameId, int threadId);

    void RemoveRoute(string gameId);

    void RemoveRoutesForThread(int threadId);

    int? ResolveRoute(string gameId);
}

