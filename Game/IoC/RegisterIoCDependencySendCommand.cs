namespace Game;

public class RegisterIoCDependencySendCommand : ICommand
{
    public void Execute()
    {
        Func<object[], object> strategy = (object[] args) =>
        {
            var command = (ICommand)args[0];
            var receiver = (IMessageReceiver)args[1];

            return new SendCommand(command, receiver);
        };

        var registerCommand = Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Send",
            strategy
        );

        registerCommand.Execute();
    }
}
