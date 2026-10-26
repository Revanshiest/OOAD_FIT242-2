using System.Reflection;

namespace Game.AdapterGenerator;

public sealed class AdapterGenerator : IAdapterGenerator
{
    private readonly ICommandScanner _commandScanner;
    private readonly IAdapterSourceBuilder _sourceBuilder;
    private readonly IAdapterCompiler _adapterCompiler;
    private readonly IDependencyRegistrar _dependencyRegistrar;
    private readonly IReadOnlyList<Assembly> _sourceAssemblies;

    public AdapterGenerator(
        ICommandScanner commandScanner,
        IAdapterSourceBuilder sourceBuilder,
        IAdapterCompiler adapterCompiler,
        IDependencyRegistrar dependencyRegistrar,
        IEnumerable<Assembly> sourceAssemblies)
    {
        _commandScanner = commandScanner;
        _sourceBuilder = sourceBuilder;
        _adapterCompiler = adapterCompiler;
        _dependencyRegistrar = dependencyRegistrar;
        _sourceAssemblies = sourceAssemblies.ToList();
    }

    public void GenerateAndRegister()
    {
        var scannedCommands = _commandScanner.Scan(_sourceAssemblies);

        scannedCommands
            .Select(command => (Command: command, AdapterType: GenerateAdapterType(command)))
            .ToList()
            .ForEach(registration =>
            {
                _dependencyRegistrar.RegisterAdapter(registration.Command, registration.AdapterType);
                _dependencyRegistrar.RegisterCommand(registration.Command);
            });
    }

    private Type GenerateAdapterType(CommandMetadata metadata)
    {
        var adapterSourceCode = _sourceBuilder.BuildAdapterSource(metadata);
        var compiledAssembly = _adapterCompiler.Compile(adapterSourceCode, _sourceAssemblies);

        return compiledAssembly.GetType(metadata.AdapterClassName)
            ?? throw new InvalidOperationException($"Compiled adapter type '{metadata.AdapterClassName}' was not found in the generated assembly.");
    }
}
