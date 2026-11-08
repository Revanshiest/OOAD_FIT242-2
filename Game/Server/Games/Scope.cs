namespace GameServer.Games;

public sealed class Scope : IScope
{
    private readonly Dictionary<string, IGameItem> _itemsById = new();
    private readonly object? _iocScope;

    public Scope(object? iocScope = null)
    {
        _iocScope = iocScope ?? TryResolveIocScope();
    }

    private static object? TryResolveIocScope()
    {
        try
        {
            return Ioc.Resolve<object>("IoC.Scope.Create");
        }
        catch
        {
            return null;
        }
    }

    public object? IocScope => _iocScope;

    public IReadOnlyCollection<string> ItemIds => _itemsById.Keys;

    public IGameItem GetItem(string itemId)
    {
        _ = !string.IsNullOrWhiteSpace(itemId)
            ? 0
            : throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(itemId));

        return _itemsById.TryGetValue(itemId, out var item)
            ? item
            : throw new KeyNotFoundException($"Item with id '{itemId}' is not present in the scope.");
    }

    public void Register(string itemId, IGameItem item)
    {
        _ = !string.IsNullOrWhiteSpace(itemId)
            ? 0
            : throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(itemId));
        ArgumentNullException.ThrowIfNull(item);

        _ = _itemsById.TryAdd(itemId, item)
            ? 0
            : throw new InvalidOperationException($"Item with id '{itemId}' already exists in the scope.");
    }

    public void Unregister(string itemId)
    {
        _ = !string.IsNullOrWhiteSpace(itemId)
            ? 0
            : throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(itemId));

        _ = _itemsById.Remove(itemId)
            ? 0
            : throw new KeyNotFoundException($"Item with id '{itemId}' is not present in the scope.");
    }

    public bool Contains(string itemId)
    {
        _ = !string.IsNullOrWhiteSpace(itemId)
            ? 0
            : throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(itemId));

        return _itemsById.ContainsKey(itemId);
    }
}
