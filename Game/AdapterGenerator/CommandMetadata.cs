using System.Reflection;

namespace Game.AdapterGenerator;

public sealed class CommandMetadata
{
    public Type CommandType { get; }
    public Type InterfaceType { get; }
    public ConstructorInfo Constructor { get; }
    public string CommandDependencyKey { get; }
    public string AdapterDependencyKey { get; }
    public string AdapterClassName { get; }

    public CommandMetadata(Type commandType, Type interfaceType, ConstructorInfo constructor)
    {
        CommandType = commandType;
        InterfaceType = interfaceType;
        Constructor = constructor;

        CommandDependencyKey = $"Commands.{SanitizeFullName(commandType)}";
        AdapterClassName = $"{SanitizeFullName(interfaceType)}Adapter";
        AdapterDependencyKey = $"Adapters.{AdapterClassName}";
    }

    private static string SanitizeFullName(Type type)
    {
        return (type.FullName ?? type.Name).Replace(".", "_");
    }
}
