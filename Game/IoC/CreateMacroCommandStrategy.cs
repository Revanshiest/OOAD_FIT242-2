namespace Game.IoC;

public sealed class CreateMacroCommandStrategy
{
    private readonly string commandSpecification;

    public CreateMacroCommandStrategy(string commandSpecification)
    {
        ArgumentNullException.ThrowIfNull(commandSpecification);

        this.commandSpecification = commandSpecification;
    }

    public ICommand Resolve(object[] arguments)
    {
        var commandNames = Ioc.Resolve<string[]>(commandSpecification);
        var commands = commandNames.Select(name => Ioc.Resolve<ICommand>(name, arguments)).ToArray();

        return Ioc.Resolve<ICommand>("Commands.Macro", new object[] { commands });
    }
}
