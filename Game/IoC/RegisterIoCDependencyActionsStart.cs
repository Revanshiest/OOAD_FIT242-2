using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyActionsStart : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => new StartCommand(
                StrategyArguments.Get<IDictionary<string, object>>(args, 0),
                StrategyArguments.Get<string>(args, 1))).Execute();
    }
}
