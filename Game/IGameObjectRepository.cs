namespace Game;

public interface IGameObjectRepository
{
    IReadOnlyDictionary<string, object> Get(string gameObjectId);

    void Add(string gameObjectId, IReadOnlyDictionary<string, object> gameObject);
}
