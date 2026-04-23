using App;
using App.Scopes;
using Game;

public class RegisterIoCDependencyRotateCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = (object[] args) =>
        {
            var obj = args[0];

            var adapter = Ioc.Resolve<object>("Adapters.IRotatable", obj);

            return new RotateCommand((IRotatable)adapter);
        };

        var registerCommand = Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Rotate",
            strategy
        );

        registerCommand.Execute();
    }
}

