using App;
using App.Scopes;
using Game;

public class RegisterIoCDependencyMoveCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = (object[] args) =>
        {
            var obj = args[0];

            var adapter = Ioc.Resolve<object>("Adapters.IMovingObject", obj);

            return new MoveCommand((IMovingObject)adapter);
        };

        var registerCommand = Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Move",
            strategy
        );

        registerCommand.Execute();
    }
}
