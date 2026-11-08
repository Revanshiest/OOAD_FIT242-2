namespace GameServer.Games.Pipeline;

using global::Game;
using global::Game.Commands;

public sealed class MoveGameItemCommand : ICommand
{
    private readonly IScope _scope;
    private readonly string _itemId;

    public MoveGameItemCommand(IScope scope, string itemId)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _itemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
    }

    public string ItemId => _itemId;

    public void Execute()
    {
        var item = _scope.GetItem(_itemId);
        IMovingObject movingObject;

        try
        {
            movingObject = Ioc.Resolve<IMovingObject>(GameServer.IoC.RegisterIoCDependencyServerItemAdapters.AdapterDependencyKey, item);
        }
        catch
        {
            movingObject = item as IMovingObject ?? new GameItemMovingObjectAdapter(item);
        }

        new MoveCommand(movingObject).Execute();
    }
}
