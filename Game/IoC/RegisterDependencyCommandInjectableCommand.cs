using Game.Commands;

namespace Game.IoC;

public sealed class RegisterDependencyCommandInjectableCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.CommandInjectable,
            (object[] args) => new CommandInjectableCommand()).Execute();
    }
}
