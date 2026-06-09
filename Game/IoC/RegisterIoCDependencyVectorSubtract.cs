using System;
using System.Linq;
using Game;

public class RegisterIoCDependencyVectorSubtract : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Math.Vector.Subtract",
            (Func<object[], object>)(args =>
            {
                var v1 = (Vector)args[0];
                var v2 = (Vector)args[1];

                return v1.Coordinates.Length == v2.Coordinates.Length
                    ? new Vector(v1.Coordinates.Zip(v2.Coordinates, (x, y) => x - y).ToArray())
                    : throw new ArgumentException("Векторы должны быть одинаковой размерности.");
            })
        ).Execute();
    }
}
