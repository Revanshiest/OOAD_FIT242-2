namespace GameServer.Games.Pipeline;

public sealed class SetGameItemVelocityCommand(IScope scope, GameItemCreationSpecification specification) : ICommand
{
    public void Execute()
    {
        scope.GetItem(specification.ItemId).SetProperty(GameItemProperty.Speed, specification.Speed);
    }
}
