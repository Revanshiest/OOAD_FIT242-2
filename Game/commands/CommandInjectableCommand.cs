public class CommandInjectableCommand : ICommand, ICommandInjectable
{
    private ICommand? _injectedCommand;

    public void Inject(ICommand command)
    {
        _injectedCommand = command ?? throw new ArgumentNullException(nameof(command));
    }

    public void Execute()
    {
        (_injectedCommand ?? throw new InvalidOperationException("Command not injected")).Execute();
    }
}
