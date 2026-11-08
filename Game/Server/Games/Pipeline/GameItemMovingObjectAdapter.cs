namespace GameServer.Games.Pipeline;

using global::Game;
using GameServer.Games;

public sealed class GameItemMovingObjectAdapter : IMovingObject
{
    private readonly IGameItem _item;

    public GameItemMovingObjectAdapter(IGameItem item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public Vector Position
    {
        get => (Vector)_item.GetProperty(GameItemProperty.Position);
        set => _item.SetProperty(GameItemProperty.Position, value);
    }

    public Vector Speed => (Vector)_item.GetProperty(GameItemProperty.Speed);
}
