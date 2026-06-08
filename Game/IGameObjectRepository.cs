namespace Game;

public interface IGameObjectRepository
{
    IDictionary<string, object> Get(string id);
    void Add(string id, IDictionary<string, object> gameObject);
}
