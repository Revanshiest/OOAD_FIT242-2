using System.Collections.Concurrent;

namespace Game;

public class GameObjectRepository : IGameObjectRepository
{
    private readonly ConcurrentDictionary<string, IDictionary<string, object>> _objects = new();

    public IDictionary<string, object> Get(string id)
    {
        if (!_objects.TryGetValue(id, out var gameObject))
        {
            throw new KeyNotFoundException($"Game object with id '{id}' not found");
        }

        return gameObject;
    }

    public void Add(string id, IDictionary<string, object> gameObject)
    {
        if (!_objects.TryAdd(id, gameObject))
        {
            throw new InvalidOperationException($"Game object with id '{id}' already exists");
        }
    }
}
