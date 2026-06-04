public class CreateMacroCommandStrategy
{
    private string spec;

    public CreateMacroCommandStrategy(string commandSpec)
    {
        spec = commandSpec;
    }
    public ICommand Resolve(object[] args)
    {
        string[] commandsNames = Ioc.Resolve<string[]>(spec);
        var commands = commandsNames.Select(name => Ioc.Resolve<ICommand>(name, args)).ToArray();

        return Ioc.Resolve<ICommand>("Commands.Macro", new object[] { (object)commands });
    }
}
