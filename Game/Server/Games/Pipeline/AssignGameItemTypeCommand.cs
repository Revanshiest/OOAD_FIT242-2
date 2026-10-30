namespace GameServer.Games.Pipeline;

public sealed class AssignGameItemTypeCommand(IScope scope, GameItemCreationSpecification specification) : ICommand
{
    public void Execute()
    {
        scope.GetItem(specification.ItemId).SetProperty(GameItemProperty.DomainType, specification.DomainType);
    }
}
