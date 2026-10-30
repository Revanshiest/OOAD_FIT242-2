using Game;
namespace GameServer.Games.Pipeline;

public sealed class MoveGameItemCommand(IScope scope, string itemId) : ICommand
{
    public void Execute()
    {
        var item = scope.GetItem(itemId);
        var currentPosition = (Vector)item.GetProperty(GameItemProperty.Position);
        var currentSpeed = (Vector)item.GetProperty(GameItemProperty.Speed);

        item.SetProperty(GameItemProperty.Position, currentPosition + currentSpeed);
    }
}
