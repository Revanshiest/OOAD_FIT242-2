namespace GameServer.Games.Pipeline;

using global::Game.Commands;
using GameServer.Games;

public sealed class CreateGameItemMacroCommand : MacroCommand
{
    public GameItemCreationSpecification Specification { get; }

    public CreateGameItemMacroCommand(IScope scope, GameItemCreationSpecification specification)
        : base(
        [
            new CreateGameItemCommand(scope, specification),
            new AssignGameItemTypeCommand(scope, specification),
            new SetGameItemPositionCommand(scope, specification),
            new SetGameItemVelocityCommand(scope, specification)
        ])
    {
        Specification = specification ?? throw new ArgumentNullException(nameof(specification));
    }
}
