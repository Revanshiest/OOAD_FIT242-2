using System;

namespace Game;

public class CheckAuthorizationCommand : ICommand
{
    private readonly string _playerId;
    private readonly string _gameObjectId;
    private readonly ICommand _innerCommand;

    public CheckAuthorizationCommand(string playerId, string gameObjectId, ICommand innerCommand)
    {
        _playerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        _gameObjectId = gameObjectId ?? throw new ArgumentNullException(nameof(gameObjectId));
        _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
    }

    public void Execute()
    {
        Ioc.Resolve<ICommand>("Auth.Check", _playerId, _gameObjectId).Execute();

        _innerCommand.Execute();
    }
}
