namespace Game.Commands;

public class MacroCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _commands;

    public MacroCommand(ICommand[] commands)
    {
        _commands = commands;
    }

    public void Execute()
    {
        _commands.ToList().ForEach(command => command.Execute());
    }
}
