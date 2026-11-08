namespace GameServer.Games;

public interface IReadOnlyGameItem
{
    IReadOnlyCollection<string> PropertyNames { get; }

    object GetProperty(string propertyName);
}

public interface IMutableGameItem : IReadOnlyGameItem
{
    void SetProperty(string propertyName, object propertyValue);
}

public interface IGameItem : IMutableGameItem
{
}
