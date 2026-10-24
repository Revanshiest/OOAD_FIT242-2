using System.Reflection;
using System.Text;

namespace Game.AdapterGenerator;

public class AdapterSourceBuilder : IAdapterSourceBuilder
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";

    public string BuildAdapterSource(CommandMetadata metadata)
    {
        var interfaceType = metadata.InterfaceType;
        var adapterClassName = metadata.AdapterClassName;

        var sb = new StringBuilder();

        AppendUsings(sb, interfaceType);
        sb.AppendLine();
        AppendClassHeader(sb, adapterClassName, interfaceType);
        AppendConstructor(sb, adapterClassName);
        AppendProperties(sb, interfaceType);
        AppendMethods(sb, interfaceType);
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendClassHeader(StringBuilder sb, string adapterClassName, Type interfaceType)
    {
        sb.AppendLine($"public class {adapterClassName} : {GetFullTypeName(interfaceType)}");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly IDictionary<string, object> _item;");
    }

    private static void AppendConstructor(StringBuilder sb, string adapterClassName)
    {
        sb.AppendLine();
        sb.AppendLine($"{Indent}public {adapterClassName}(IDictionary<string, object> item)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_item = item;");
        sb.AppendLine($"{Indent}}}");
    }

    private static void AppendUsings(StringBuilder sb, Type interfaceType)
    {
        var namespaces = new HashSet<string> { "App", "System.Collections.Generic" };

        AddTypeNamespaces(interfaceType, namespaces);

        foreach (var prop in GetAllProperties(interfaceType))
        {
            AddTypeNamespaces(prop.PropertyType, namespaces);
        }

        foreach (var method in GetAllMethods(interfaceType))
        {
            AddTypeNamespaces(method.ReturnType, namespaces);

            foreach (var param in method.GetParameters())
            {
                AddTypeNamespaces(param.ParameterType, namespaces);
            }
        }

        foreach (var ns in namespaces.OrderBy(n => n))
        {
            sb.AppendLine($"using {ns};");
        }
    }

    private static void AddTypeNamespaces(Type type, HashSet<string> namespaces)
    {
        namespaces.UnionWith(
            new[] { type.Namespace }
                .OfType<string>()
                .Where(ns => ns.Length > 0));

        foreach (var arg in type.GetGenericArguments())
        {
            AddTypeNamespaces(arg, namespaces);
        }
    }

    private static void AppendProperties(StringBuilder sb, Type interfaceType)
    {
        foreach (var property in GetAllProperties(interfaceType))
        {
            sb.AppendLine();
            var typeName = GetFullTypeName(property.PropertyType);

            sb.AppendLine($"{Indent}public {typeName} {property.Name}");
            sb.AppendLine($"{Indent}{{");
            AppendPropertyAccessors(sb, property, typeName);
            sb.AppendLine($"{Indent}}}");
        }
    }

    private static void AppendPropertyAccessors(StringBuilder sb, PropertyInfo property, string typeName)
    {
        var accessors = new (bool enabled, string code)[]
        {
            (property.CanRead, $"{DoubleIndent}get => Ioc.Resolve<{typeName}>(\"Properties.{property.Name}\", _item);"),
            (property.CanWrite, $"{DoubleIndent}set => Ioc.Resolve<ICommand>(\"Properties.{property.Name}.Set\", _item, value).Execute();")
        };

        foreach (var (_, code) in accessors.Where(a => a.enabled))
        {
            sb.AppendLine(code);
        }
    }

    private static void AppendMethods(StringBuilder sb, Type interfaceType)
    {
        foreach (var method in GetAllMethods(interfaceType))
        {
            sb.AppendLine();
            var returnTypeName = GetFullTypeName(method.ReturnType);
            var parameters = method.GetParameters();
            var paramDefs = string.Join(", ", parameters.Select(p => $"{GetFullTypeName(p.ParameterType)} {p.Name}"));
            var paramArgs = parameters.Length > 0
                ? ", " + string.Join(", ", parameters.Select(p => p.Name))
                : "";

            var dependencyKey = $"{interfaceType.Name}.{method.Name}";

            sb.AppendLine($"{Indent}public {returnTypeName} {method.Name}({paramDefs})");
            sb.AppendLine($"{Indent}{{");

            sb.AppendLine(method.ReturnType == typeof(void)
                ? $"{DoubleIndent}Ioc.Resolve<ICommand>(\"{dependencyKey}\", _item{paramArgs}).Execute();"
                : $"{DoubleIndent}return Ioc.Resolve<{returnTypeName}>(\"{dependencyKey}\", _item{paramArgs});");

            sb.AppendLine($"{Indent}}}");
        }
    }

    private static T[] CollectInterfaceMembers<T, TKey>(
        Type interfaceType,
        Func<Type, IEnumerable<T>> memberSelector,
        Func<T, TKey> deduplicationKey)
    {
        return interfaceType.GetInterfaces()
            .Prepend(interfaceType)
            .SelectMany(memberSelector)
            .GroupBy(deduplicationKey)
            .Select(g => g.First())
            .ToArray();
    }

    private static PropertyInfo[] GetAllProperties(Type interfaceType)
    {
        return CollectInterfaceMembers(
            interfaceType,
            t => t.GetProperties(),
            p => p.Name);
    }

    private static MethodInfo[] GetAllMethods(Type interfaceType)
    {
        return CollectInterfaceMembers(
            interfaceType,
            t => t.GetMethods().Where(m => !m.IsSpecialName),
            m => (m.Name, string.Join(",", m.GetParameters().Select(p => p.ParameterType.FullName))));
    }

    private static string GetFullTypeName(Type type)
    {
        return type == typeof(void) ? "void"
            : type.IsGenericType ? FormatGenericTypeName(type)
            : type.FullName ?? type.Name;
    }

    private static string FormatGenericTypeName(Type type)
    {
        var genericDef = type.GetGenericTypeDefinition();
        var baseName = (genericDef.FullName ?? genericDef.Name).Split('`')[0];
        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFullTypeName));
        return $"{baseName}<{genericArgs}>";
    }
}
