using Game.Commands;

namespace GameServer.Games.Pipeline;

public sealed class CreateGameItemPipeline(IScope scope, GameItemCreationSpecification specification)
{
    public MacroCommand Build()
    {
        return new MacroCommand(
        [
            new CreateGameItemCommand(scope, specification),
            new AssignGameItemTypeCommand(scope, specification),
            new SetGameItemPositionCommand(scope, specification),
            new SetGameItemVelocityCommand(scope, specification)
        ]);
    }
}
