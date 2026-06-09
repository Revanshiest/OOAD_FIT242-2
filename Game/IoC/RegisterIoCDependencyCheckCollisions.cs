using System;
using Game;

public class RegisterIoCDependencyCheckCollisions : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.DetectCollision",
            (Func<object[], object>)(args =>
                new DetectCollisionCommand(
                    (ICollisionObject)args[0],
                    (ICollisionObject)args[1])
            )).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.CheckCollisions",
            (Func<object[], object>)(args =>
            {
                var adapter = Ioc.Resolve<ICollisionObject>("Adapters.ICollisionObject", args[0]);
                return new CheckAllCollisionsCommand(adapter);
            })).Execute();
    }
}
