namespace GameServer.Games.Pipeline;

public sealed class CreateGameItemPipeline(IScope scope, GameItemCreationSpecification specification)
{
    public CreateGameItemMacroCommand Build()
    {
        return new CreateGameItemMacroCommand(scope, specification);
    }
}
