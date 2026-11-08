using System.Collections.Concurrent;

namespace GameServer.Resharding;

public sealed record GameObjectSnapshot(string ItemId, string SerializedProperties);

public sealed record GameSnapshot(
    string GameId,
    GameServer.Games.GamePriority Priority,
    IReadOnlyList<GameObjectSnapshot> Items,
    IReadOnlyList<string>? QueuedCommands = null);

public interface ISnapshotCodec
{
    string Serialize(GameSnapshot snapshot);

    GameSnapshot Deserialize(string payload);
}

public interface ISnapshotStore
{
    void Save(GameSnapshot snapshot);

    int SavedSnapshotCount { get; }

    IReadOnlyList<GameSnapshot> LoadAll();
}

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<string, GameSnapshot> _snapshotsByGameId = new();

    public int SavedSnapshotCount => _snapshotsByGameId.Count;

    public void Save(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshotsByGameId[snapshot.GameId] = snapshot;
    }

    public IReadOnlyList<GameSnapshot> LoadAll()
    {
        return _snapshotsByGameId.Values.ToList();
    }
}
