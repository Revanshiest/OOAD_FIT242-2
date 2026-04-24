public class RegisterIoCDependencyActionsStart : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => Ioc.Resolve<ICommand>("Commands.Macro", args[0])
        ).Execute();
    }
}
