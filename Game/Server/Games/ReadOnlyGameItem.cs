namespace GameServer.Games;

public sealed class ReadOnlyGameItem : IGameItem
{
    private readonly Dictionary<string, object> _properties;

    public ReadOnlyGameItem(IReadOnlyDictionary<string, object> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _properties = new Dictionary<string, object>(properties);
    }

    public IReadOnlyCollection<string> PropertyNames => _properties.Keys;

    public object GetProperty(string propertyName)
    {
        return _properties.TryGetValue(propertyName, out var value)
            ? value
            : throw new KeyNotFoundException($"Property '{propertyName}' was not found on the read-only item.");
    }

    public void SetProperty(string propertyName, object propertyValue)
    {
        throw new NotSupportedException($"Cannot modify read-only game item property '{propertyName}'.");
    }
}
