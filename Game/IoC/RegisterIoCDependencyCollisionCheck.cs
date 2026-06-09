using System;
using System.Collections.Generic;
using Game;

public class RegisterIoCDependencyCollisionCheck : ICommand
{
    private readonly HashSet<(int, int, int, int)> _collisionData;

    public RegisterIoCDependencyCollisionCheck(HashSet<(int, int, int, int)> collisionData)
    {
        _collisionData = collisionData ?? throw new ArgumentNullException(nameof(collisionData));
    }

    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Check",
            (Func<object[], object>)(args =>
            {
                var deltaPos = (Vector)args[0];
                var deltaVel = (Vector)args[1];

                return _collisionData.Contains((
                    deltaPos.Coordinates[0], deltaPos.Coordinates[1],
                    deltaVel.Coordinates[0], deltaVel.Coordinates[1]));
            })
        ).Execute();
    }
}
