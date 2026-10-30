namespace GameServer.Games;

public interface IScope
{
    IGameItem GetItem(string itemId);

    void Register(string itemId, IGameItem item);

    void Unregister(string itemId);

    bool Contains(string itemId);

    IReadOnlyCollection<string> ItemIds { get; }
}
