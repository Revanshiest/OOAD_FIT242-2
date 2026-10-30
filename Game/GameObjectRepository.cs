using System.Collections.Concurrent;

namespace Game;

public sealed class GameObjectRepository : IGameObjectRepository
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _gameObjects = new();

    public IReadOnlyDictionary<string, object> Get(string gameObjectId)
    {
        if (string.IsNullOrWhiteSpace(gameObjectId))
        {
            throw new ArgumentException("Game object identifier is required.", nameof(gameObjectId));
        }

        if (!_gameObjects.TryGetValue(gameObjectId, out var gameObject))
        {
            throw new KeyNotFoundException($"Game object with id '{gameObjectId}' not found.");
        }

        return gameObject.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public void Add(string gameObjectId, IReadOnlyDictionary<string, object> gameObject)
    {
        if (string.IsNullOrWhiteSpace(gameObjectId))
        {
            throw new ArgumentException("Game object identifier is required.", nameof(gameObjectId));
        }

        ArgumentNullException.ThrowIfNull(gameObject);

        var snapshot = gameObject.ToDictionary(pair => pair.Key, pair => pair.Value);

        if (!_gameObjects.TryAdd(gameObjectId, snapshot))
        {
            throw new InvalidOperationException($"Game object with id '{gameObjectId}' already exists.");
        }
    }
}
