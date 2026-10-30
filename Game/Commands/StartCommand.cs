namespace Game.Commands;

public sealed class StartCommand : ICommand
{
    private readonly IDictionary<string, object> _gameObject;
    private readonly string _commandType;

    public StartCommand(IDictionary<string, object> gameObject, string commandType)
    {
        _gameObject = gameObject;
        _commandType = commandType;
    }

    public void Execute()
    {
        var baseCommand = Ioc.Resolve<ICommand>($"Commands.{_commandType}", _gameObject);
        var commandInjectable = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");
        var messageReceiver = Ioc.Resolve<IMessageReceiver>("Game.CommandsReceiver");
        var initialSendCommand = new SendCommand((ICommand)commandInjectable, messageReceiver);
        var repeatableCommand = Ioc.Resolve<ICommand>($"Macro.{_commandType}", baseCommand, initialSendCommand);

        commandInjectable.Inject(repeatableCommand);
        _gameObject[$"repeatable{_commandType}"] = commandInjectable;

        new SendCommand(repeatableCommand, messageReceiver).Execute();
    }
}
