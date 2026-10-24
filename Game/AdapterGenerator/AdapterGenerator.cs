using System.Reflection;

namespace Game.AdapterGenerator;

public class AdapterGenerator : IAdapterGenerator
{
    private readonly ICommandScanner _scanner;
    private readonly IAdapterSourceBuilder _sourceBuilder;
    private readonly IAdapterCompiler _compiler;
    private readonly IDependencyRegistrar _registrar;
    private readonly IReadOnlyList<Assembly> _assemblies;

    public AdapterGenerator(
        ICommandScanner scanner,
        IAdapterSourceBuilder sourceBuilder,
        IAdapterCompiler compiler,
        IDependencyRegistrar registrar,
        IEnumerable<Assembly> assemblies)
    {
        _scanner = scanner;
        _sourceBuilder = sourceBuilder;
        _compiler = compiler;
        _registrar = registrar;
        _assemblies = assemblies.ToList();
    }

    public void GenerateAndRegister()
    {
        var commands = _scanner.Scan(_assemblies);

        foreach (var command in commands)
        {
            var adapterType = GenerateAdapter(command);
            _registrar.RegisterAdapter(command, adapterType);
            _registrar.RegisterCommand(command);
        }
    }

    private Type GenerateAdapter(CommandMetadata metadata)
    {
        var sourceCode = _sourceBuilder.BuildAdapterSource(metadata);
        var compiledAssembly = _compiler.Compile(sourceCode, _assemblies);

        return compiledAssembly.GetType(metadata.AdapterClassName)
            ?? throw new InvalidOperationException(
                $"Скомпилированный тип '{metadata.AdapterClassName}' не найден в сборке.");
    }
}
