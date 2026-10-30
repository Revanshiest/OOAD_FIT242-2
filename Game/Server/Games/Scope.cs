namespace GameServer.Games;

public sealed class Scope : IScope
{
    private readonly Dictionary<string, IGameItem> _itemsById = new();

    public IReadOnlyCollection<string> ItemIds => _itemsById.Keys;

    public IGameItem GetItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item identifier is required.", nameof(itemId));
        }

        if (!_itemsById.TryGetValue(itemId, out var item))
        {
            throw new KeyNotFoundException($"Item with id '{itemId}' is not present in the scope.");
        }

        return item;
    }

    public void Register(string itemId, IGameItem item)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item identifier is required.", nameof(itemId));
        }

        ArgumentNullException.ThrowIfNull(item);

        if (!_itemsById.TryAdd(itemId, item))
        {
            throw new InvalidOperationException($"Item with id '{itemId}' already exists in the scope.");
        }
    }

    public void Unregister(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item identifier is required.", nameof(itemId));
        }

        if (!_itemsById.Remove(itemId))
        {
            throw new KeyNotFoundException($"Item with id '{itemId}' is not present in the scope.");
        }
    }

    public bool Contains(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item identifier is required.", nameof(itemId));
        }

        return _itemsById.ContainsKey(itemId);
    }
}
