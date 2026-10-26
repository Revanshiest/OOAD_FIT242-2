namespace Game.Commands;

public class StopCommand : ICommand
{
    private readonly ICommandInjectable _injectable;

    public StopCommand(ICommandInjectable injectable)
    {
        _injectable = injectable ?? throw new ArgumentNullException(nameof(injectable));
    }

    public void Execute()
    {
        _injectable.Inject(new EmptyCommand());
    }
}
