using System.Collections.Concurrent;

namespace GameServer.Resharding;

public sealed class ReshardingTable : IReshardingTable
{
    private int _threadCount;
    private readonly ConcurrentDictionary<string, int> _threadIdByGameId = new();

    public ReshardingTable(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1, nameof(threadCount));
        _threadCount = threadCount;
    }

    public int ThreadCount => Volatile.Read(ref _threadCount);

    public void UpdateThreadCount(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1, nameof(threadCount));
        Interlocked.Exchange(ref _threadCount, threadCount);
    }

    public void SetRoute(string gameId, int threadId)
    {
        _ = !string.IsNullOrWhiteSpace(gameId)
            ? 0
            : throw new ArgumentException("Game ID cannot be null or whitespace.", nameof(gameId));

        var currentThreadCount = Volatile.Read(ref _threadCount);
        ArgumentOutOfRangeException.ThrowIfNegative(threadId, nameof(threadId));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(threadId, currentThreadCount, nameof(threadId));

        _threadIdByGameId[gameId] = threadId;
    }

    public void RemoveRoute(string gameId)
    {
        _ = !string.IsNullOrWhiteSpace(gameId)
            ? 0
            : throw new ArgumentException("Game ID cannot be null or whitespace.", nameof(gameId));

        _threadIdByGameId.TryRemove(gameId, out _);
    }

    public void RemoveRoutesForThread(int threadId)
    {
        var routesToRemove = _threadIdByGameId
            .Where(pair => pair.Value == threadId)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var gameId in routesToRemove)
        {
            _threadIdByGameId.TryRemove(gameId, out _);
        }
    }

    public int? ResolveRoute(string gameId)
    {
        return !string.IsNullOrWhiteSpace(gameId) && _threadIdByGameId.TryGetValue(gameId, out var threadId)
            ? threadId
            : null;
    }

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        return _threadIdByGameId.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
