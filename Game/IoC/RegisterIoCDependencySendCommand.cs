using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencySendCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = args => new SendCommand(
            StrategyArguments.Get<ICommand>(args, 0),
            StrategyArguments.Get<IMessageReceiver>(args, 1));

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Send", strategy).Execute();
    }
}
