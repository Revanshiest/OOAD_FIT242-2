using Game.Commands;

namespace Game.IoC;

public sealed class RegisterIoCDependencyActionsStop : ICommand
{
    private const string OrderGameObjectField = "GameObject";
    private const string OrderCommandTypeField = "CmdType";

    public void Execute()
    {
        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.ActionsStop, (object[] args) =>
        {
            var order = StrategyArguments.Get<IDictionary<string, object>>(args, 0);
            var gameObject = (IDictionary<string, object>)order[OrderGameObjectField];
            var commandType = (string)order[OrderCommandTypeField];
            var repeatableKey = $"{IocDependencyKeys.RepeatableCommandPrefix}{commandType}";
            var injectable = (ICommandInjectable)gameObject[repeatableKey];

            return new StopCommand(injectable);
        }).Execute();
    }
}
