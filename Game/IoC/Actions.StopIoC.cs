public class RegisterIoCDependencyActionsStop : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Stop",
            (object[] args) => new EmptyCommand()
        ).Execute();
    }
}
