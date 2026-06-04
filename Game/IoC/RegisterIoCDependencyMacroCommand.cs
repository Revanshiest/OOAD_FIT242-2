using App;
using App.Scopes;

public class RegisterIoCDependencyMacroCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = (object[] args) =>
        {
            var commands = (ICommand[])args[0];

            return new MacroCommand(commands);
        };

        var registerCommand = Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Macro",
            strategy
        );

        registerCommand.Execute();
    }
}
