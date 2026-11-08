namespace GameServer.Games.Pipeline;

public sealed class CreateGameItemCommand(IScope scope, GameItemCreationSpecification specification) : ICommand
{
    public GameItemCreationSpecification Specification => specification;

    public void Execute()
    {
        scope.Register(specification.ItemId, new GameItem());
    }
}
