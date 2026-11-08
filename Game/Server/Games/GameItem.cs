namespace GameServer.Games;

public sealed class GameItem : IGameItem
{
    private readonly Dictionary<string, object> _properties = new();

    public IReadOnlyCollection<string> PropertyNames => _properties.Keys;

    public object GetProperty(string propertyName)
    {
        _ = !string.IsNullOrWhiteSpace(propertyName)
            ? 0
            : throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));

        return _properties.TryGetValue(propertyName, out var propertyValue)
            ? propertyValue
            : throw new KeyNotFoundException($"Property '{propertyName}' is not present on the game item.");
    }

    public void SetProperty(string propertyName, object propertyValue)
    {
        _ = !string.IsNullOrWhiteSpace(propertyName)
            ? 0
            : throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
        ArgumentNullException.ThrowIfNull(propertyValue);

        _properties[propertyName] = propertyValue;
    }
}
