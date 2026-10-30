namespace GameServer.Games.Pipeline;

public sealed class SetGameItemPositionCommand(IScope scope, GameItemCreationSpecification specification) : ICommand
{
    public void Execute()
    {
        scope.GetItem(specification.ItemId).SetProperty(GameItemProperty.Position, specification.Position);
    }
}
