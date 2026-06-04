using System;
using System.Collections.Generic;

namespace Game;

public class GameCommand : ICommand
{
    private readonly IDictionary<string, object> _order;

    public GameCommand(IDictionary<string, object> order)
    {
        _order = order ?? throw new ArgumentNullException(nameof(order));
    }

    public void Execute()
    {
        var playerId = (string)_order["PlayerId"];
        var gameObjectId = (string)_order["GameObjectId"];
        var cmdType = (string)_order["CmdType"];

        var repository = Ioc.Resolve<IGameObjectRepository>("Repository.GameObject");
        var gameObject = repository.Get(gameObjectId);

        var action = Ioc.Resolve<ICommand>($"Actions.{cmdType}", gameObject, cmdType);
        var authorizedAction = new CheckAuthorizationCommand(playerId, gameObjectId, action);
        authorizedAction.Execute();
    }
}
