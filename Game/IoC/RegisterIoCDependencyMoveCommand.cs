using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyMoveCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = args =>
        {
            var gameObject = StrategyArguments.Get<object>(args, 0);
            var adapter = Ioc.Resolve<object>(IocDependencyKeys.MovingObjectAdapter, gameObject);

            return new MoveCommand((IMovingObject)adapter);
        };

        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.MoveCommand, strategy).Execute();
    }
}
