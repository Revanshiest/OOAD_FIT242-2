using System;
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
        Vector deltaPosition = SubstractVectors(_leftObject.Position, _rightObject.Position);
        Vector deltaVelocity = SubstractVectors(_leftObject.Velocity, _rightObject.Velocity);

        bool isCollided = Ioc.Resolve<bool>("Game.Collision.Check", deltaPosition, deltaVelocity);

        if (isCollided)
        {
            Ioc.Resolve<ICommand>("Game.Collision.Event", _leftObject, _rightObject).Execute();
        }
    }

    private Vector SubstractVectors(Vector v1, Vector v2)
    {
        if (v1.Coordinates.Length != v2.Coordinates.Length)
        {
            throw new ArgumentException("Векторы должны быть одинаковой размерности.");
        }

        int[] deltaCoords = new int[v1.Coordinates.Length];
        for (int i = 0; i < v1.Coordinates.Length; i++)
        {
            deltaCoords[i] = v1.Coordinates[i] - v2.Coordinates[i];
        }

        return new Vector(deltaCoords);
    }
}
