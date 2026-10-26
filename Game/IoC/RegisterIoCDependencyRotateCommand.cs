using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyRotateCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = args =>
        {
            var gameObject = StrategyArguments.Get<object>(args, 0);
            var adapter = Ioc.Resolve<object>(IocDependencyKeys.RotatableAdapter, gameObject);

            return new RotateCommand((IRotatable)adapter);
        };

        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.RotateCommand, strategy).Execute();
    }
}
