using System.Reflection;
using Game.AdapterGenerator;

namespace Game.IoC;

public class RegisterIoCDependencyAdapterGenerator : ICommand
{
    private readonly IEnumerable<Assembly> _assemblies;

    public RegisterIoCDependencyAdapterGenerator(IEnumerable<Assembly> assemblies)
    {
        _assemblies = assemblies;
    }

    public void Execute()
    {
        var generator = new Game.AdapterGenerator.AdapterGenerator(
            new CommandScanner(),
            new AdapterSourceBuilder(),
            new RoslynAdapterCompiler(),
            new IoCDependencyRegistrar(),
            _assemblies
        );

        generator.GenerateAndRegister();
    }
}
