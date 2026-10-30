using Game.Commands;

namespace Game.IoC;

public sealed class RegisterDependencyCommandInjectableCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.CommandInjectable",
            (object[] args) => new CommandInjectableCommand()).Execute();
    }
}
