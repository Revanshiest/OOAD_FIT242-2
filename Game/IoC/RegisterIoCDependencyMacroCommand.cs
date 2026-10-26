using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyMacroCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = args => new MacroCommand(StrategyArguments.Get<ICommand[]>(args, 0));

        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.MacroCommand, strategy).Execute();
    }
}
