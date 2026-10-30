using System.Reflection;
using System.Text;

namespace Game.AdapterGenerator;

public sealed class AdapterSourceBuilder : IAdapterSourceBuilder
{
    public string BuildAdapterSource(CommandMetadata metadata)
    {
        var sourceLines = CollectImportedNamespaces(metadata.InterfaceType)
            .Select(namespaceName => $"using {namespaceName};")
            .Append(string.Empty)
            .Concat(BuildClassLines(metadata))
            .Append("}");

        return string.Join(Environment.NewLine, sourceLines);
    }

    private static IEnumerable<string> BuildClassLines(CommandMetadata metadata) =>
        new[]
        {
            $"public sealed class {metadata.AdapterClassName}(IDictionary<string, object> item) : {GetFullTypeName(metadata.InterfaceType)}",
            "{"
        }
        .Concat(CollectProperties(metadata.InterfaceType).SelectMany(BuildPropertyLines))
        .Concat(CollectMethods(metadata.InterfaceType).SelectMany(method => BuildMethodLines(metadata.InterfaceType, method)));

    private static IEnumerable<string> BuildPropertyLines(PropertyInfo property)
    {
        var propertyTypeName = GetFullTypeName(property.PropertyType);
        var propertyAccessors = new (bool IsEnabled, string Definition)[]
        {
            (property.CanRead, $"get => Ioc.Resolve<{propertyTypeName}>(\"Properties.{property.Name}\", item);"),
            (property.CanWrite, $"set => Ioc.Resolve<ICommand>(\"Properties.{property.Name}.Set\", item, value).Execute();")
        };

        return new[]
            {
                string.Empty,
                $"    public {propertyTypeName} {property.Name}",
                "    {"
            }
            .Concat(propertyAccessors
                .Where(accessor => accessor.IsEnabled)
                .Select(accessor => $"        {accessor.Definition}"))
            .Append("    }");
    }

    private static IEnumerable<string> BuildMethodLines(Type interfaceType, MethodInfo method)
    {
        var returnTypeName = GetFullTypeName(method.ReturnType);
        var parameters = method.GetParameters();
        var parameterDefinitions = string.Join(", ", parameters.Select(parameter => $"{GetFullTypeName(parameter.ParameterType)} {parameter.Name}"));
        var parameterArguments = parameters.Length > 0
            ? ", " + string.Join(", ", parameters.Select(parameter => parameter.Name))
            : string.Empty;
        var dependencyKey = $"{interfaceType.Name}.{method.Name}";
        var methodBody = method.ReturnType == typeof(void)
            ? $"Ioc.Resolve<ICommand>(\"{dependencyKey}\", item{parameterArguments}).Execute();"
            : $"return Ioc.Resolve<{returnTypeName}>(\"{dependencyKey}\", item{parameterArguments});";

        return new[]
        {
            string.Empty,
            $"    public {returnTypeName} {method.Name}({parameterDefinitions})",
            "    {",
            $"        {methodBody}",
            "    }"
        };
    }

    private static IReadOnlyCollection<string> CollectImportedNamespaces(Type interfaceType)
    {
        var referencedNamespaces = GetTypeNamespaces(interfaceType)
            .Concat(CollectProperties(interfaceType).SelectMany(property => GetTypeNamespaces(property.PropertyType)))
            .Concat(CollectMethods(interfaceType).SelectMany(method => GetTypeNamespaces(method.ReturnType)))
            .Concat(CollectMethods(interfaceType).SelectMany(method => method.GetParameters().SelectMany(parameter => GetTypeNamespaces(parameter.ParameterType))));

        return new[] { "App", "System.Collections.Generic" }
            .Concat(referencedNamespaces)
            .OfType<string>()
            .Distinct()
            .OrderBy(namespaceName => namespaceName)
            .ToArray();
    }

    private static IEnumerable<string> GetTypeNamespaces(Type type) =>
        new[] { type.Namespace }.OfType<string>()
            .Concat(type.GetGenericArguments().SelectMany(GetTypeNamespaces));

    private static PropertyInfo[] CollectProperties(Type interfaceType) =>
        CollectInterfaceMembers(interfaceType, type => type.GetProperties(), property => property.Name);

    private static MethodInfo[] CollectMethods(Type interfaceType) =>
        CollectInterfaceMembers(
            interfaceType,
            type => type.GetMethods().Where(method => !method.IsSpecialName),
            method => (method.Name, string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))));

    private static T[] CollectInterfaceMembers<T, TKey>(
        Type interfaceType,
        Func<Type, IEnumerable<T>> memberSelector,
        Func<T, TKey> deduplicationKey) =>
        interfaceType.GetInterfaces()
            .Prepend(interfaceType)
            .SelectMany(memberSelector)
            .GroupBy(deduplicationKey)
            .Select(group => group.First())
            .ToArray();

    private static string GetFullTypeName(Type type)
    {
        return type == typeof(void) ? "void"
            : type.IsGenericType ? FormatGenericTypeName(type)
            : type.FullName ?? type.Name;
    }

    private static string FormatGenericTypeName(Type type)
    {
        var genericDefinition = type.GetGenericTypeDefinition();
        var baseName = (genericDefinition.FullName ?? genericDefinition.Name).Split('`')[0];
        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetFullTypeName));

        return $"{baseName}<{genericArguments}>";
    }
}
