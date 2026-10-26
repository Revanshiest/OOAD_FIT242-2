namespace Game.Commands;

public sealed class MacroCommand : ICommand
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
