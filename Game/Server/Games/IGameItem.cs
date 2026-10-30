namespace GameServer.Games;

public interface IGameItem
{
    object GetProperty(string propertyName);

    void SetProperty(string propertyName, object propertyValue);
}
