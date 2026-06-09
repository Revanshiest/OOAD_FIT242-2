using System;
using System.Collections.Generic;
using System.Linq;
using Game;

public class CheckAllCollisionsCommand : ICommand
{
    private readonly ICollisionObject _currentObject;

    public CheckAllCollisionsCommand(ICollisionObject currentObject)
    {
        _currentObject = currentObject ?? throw new ArgumentNullException(nameof(currentObject));
    }

    public void Execute()
    {
        var allObjects = Ioc.Resolve<IEnumerable<ICollisionObject>>("Game.Objects.Collidable");

        allObjects
            .Where(other => !ReferenceEquals(other, _currentObject))
            .Select(other => Ioc.Resolve<ICommand>("Commands.DetectCollision", _currentObject, other))
            .ToList()
            .ForEach(cmd => cmd.Execute());
    }
}
