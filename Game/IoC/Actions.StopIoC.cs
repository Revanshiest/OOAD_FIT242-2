public class RegisterIoCDependencyActionsStop : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Stop",
            (object[] args) =>
            {
                var order = (IDictionary<string, object>)args[0];
                var gameObject = (IDictionary<string, object>)order["GameObject"];
                var cmdType = (string)order["CmdType"];
                var injectable = (ICommandInjectable)gameObject[$"repeatable{cmdType}"];
                return new StopCommand(injectable);
            }
        ).Execute();
    }
}
