namespace GameServer.Games;

public interface IGameItem
{
    IReadOnlyCollection<string> PropertyNames { get; }

    object GetProperty(string propertyName);

    void SetProperty(string propertyName, object propertyValue);
}
