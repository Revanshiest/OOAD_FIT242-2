using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyActionsStop : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Stop", (object[] args) =>
        {
            var order = StrategyArguments.Get<IDictionary<string, object>>(args, 0);
            var gameObject = (IDictionary<string, object>)order["GameObject"];
            var commandType = (string)order["CmdType"];
            var repeatableKey = $"repeatable{commandType}";
            var injectable = (ICommandInjectable)gameObject[repeatableKey];

            return new StopCommand(injectable);
        }).Execute();
    }
}
