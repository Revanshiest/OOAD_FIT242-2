using System;
using System.Collections.Generic;
using Game;

public class DetectCollisionCommand : ICommand
{
    private readonly ICollisionObject _leftObject;
    private readonly ICollisionObject _rightObject;

    public DetectCollisionCommand(ICollisionObject leftObject, ICollisionObject rightObject)
    {
        _leftObject = leftObject ?? throw new ArgumentNullException(nameof(leftObject));
        _rightObject = rightObject ?? throw new ArgumentNullException(nameof(rightObject));
    }

    public void Execute()
    {
        Vector deltaPosition = Ioc.Resolve<Vector>("Math.Vector.Subtract", _leftObject.Position, _rightObject.Position);
        Vector deltaVelocity = Ioc.Resolve<Vector>("Math.Vector.Subtract", _leftObject.Velocity, _rightObject.Velocity);

        bool isCollided = Ioc.Resolve<bool>("Game.Collision.Check", deltaPosition, deltaVelocity);

        new Dictionary<bool, Action>
        {
            { true, () => Ioc.Resolve<ICommand>("Game.Collision.Event", _leftObject, _rightObject).Execute() },
            { false, () => { } }
        }[isCollided]();
    }
}
