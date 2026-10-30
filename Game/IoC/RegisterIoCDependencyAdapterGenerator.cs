using System.Reflection;
using Game.AdapterGenerator;

namespace Game.IoC;

public sealed class RegisterIoCDependencyAdapterGenerator : ICommand
{
    private readonly IReadOnlyList<Assembly> _assemblies;

    public RegisterIoCDependencyAdapterGenerator(IEnumerable<Assembly> assemblies)
    {
        _assemblies = assemblies.ToList();
    }

    public void Execute()
    {
        var adapterGenerator = new Game.AdapterGenerator.AdapterGenerator(
            new CommandScanner(),
            new AdapterSourceBuilder(),
            new RoslynAdapterCompiler(),
            new IoCDependencyRegistrar(),
            _assemblies);

        adapterGenerator.GenerateAndRegister();
    }
}
