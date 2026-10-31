using System.Collections.Concurrent;

namespace GameServer.Resharding;

public sealed class ReshardingTable
{
    private readonly int _threadCount;
    private readonly ConcurrentDictionary<string, int> _threadIdByGameId = new();

    public ReshardingTable(int threadCount)
    {
        if (threadCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threadCount), threadCount, "Thread count must be positive.");
        }

        _threadCount = threadCount;
    }

    public void SetRoute(string gameId, int threadId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        if (threadId < 0 || threadId >= _threadCount)
        {
            throw new ArgumentOutOfRangeException(nameof(threadId), threadId, $"Thread identifier must be within [0, {_threadCount}).");
        }

        _threadIdByGameId[gameId] = threadId;
    }

    public void RemoveRoute(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        _threadIdByGameId.TryRemove(gameId, out _);
    }

    public int? ResolveRoute(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        return _threadIdByGameId.TryGetValue(gameId, out var threadId) ? threadId : null;
    }

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        return _threadIdByGameId.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
