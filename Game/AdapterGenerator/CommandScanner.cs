using System.Reflection;

namespace Game.AdapterGenerator;

public sealed class CommandScanner : ICommandScanner
{
    private static readonly Type CommandInterfaceType = typeof(ICommand);

    public IReadOnlyList<CommandMetadata> Scan(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(GetExportedTypes)
            .Where(IsValidCommandType)
            .SelectMany(type =>
                FindInterfaceParameterConstructors(type)
                    .Select(constructor => new CommandMetadata(
                        type,
                        constructor.GetParameters()[0].ParameterType,
                        constructor)))
            .ToList();
    }

    private static Type[] GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }

    private static bool IsValidCommandType(Type type)
    {
        return type.IsClass
            && !type.IsAbstract
            && type.Name.EndsWith("Command")
            && CommandInterfaceType.IsAssignableFrom(type);
    }

    private static IEnumerable<ConstructorInfo> FindInterfaceParameterConstructors(Type type)
    {
        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => constructor.GetParameters() is [var parameter] && parameter.ParameterType.IsInterface)
            .Take(1);
    }
}
