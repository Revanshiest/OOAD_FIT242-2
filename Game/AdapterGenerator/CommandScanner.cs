using System.Reflection;

namespace Game.AdapterGenerator;

public class CommandScanner : ICommandScanner
{
    private static readonly Type _commandInterfaceType = typeof(ICommand);

    public IReadOnlyList<CommandMetadata> Scan(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(GetExportedTypes)
            .Where(IsValidCommandType)
            .SelectMany(type =>
                FindInterfaceParameterConstructors(type)
                    .Select(ctor => new CommandMetadata(
                        type,
                        ctor.GetParameters()[0].ParameterType,
                        ctor)))
            .ToList();
    }

    private static Type[] GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>().ToArray();
        }
    }

    private static bool IsValidCommandType(Type type)
    {
        return type.IsClass
            && !type.IsAbstract
            && type.Name.EndsWith("Command")
            && _commandInterfaceType.IsAssignableFrom(type);
    }

    private static IEnumerable<ConstructorInfo> FindInterfaceParameterConstructors(Type type)
    {
        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(ctor => ctor.GetParameters() is [var param] && param.ParameterType.IsInterface)
            .Take(1);
    }
}
