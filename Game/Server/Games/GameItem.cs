namespace GameServer.Games;

public sealed class GameItem : IGameItem
{
    private readonly Dictionary<string, object> _properties = new();

    public IReadOnlyCollection<string> PropertyNames => _properties.Keys;

    public object GetProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name is required.", nameof(propertyName));
        }

        if (!_properties.TryGetValue(propertyName, out var propertyValue))
        {
            throw new KeyNotFoundException($"Property '{propertyName}' is not present on the game item.");
        }

        return propertyValue;
    }

    public void SetProperty(string propertyName, object propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name is required.", nameof(propertyName));
        }

        ArgumentNullException.ThrowIfNull(propertyValue);

        _properties[propertyName] = propertyValue;
    }
}
