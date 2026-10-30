using System.Linq.Expressions;
using System.Reflection;

namespace Game.AdapterGenerator;

public sealed class IoCDependencyRegistrar : IDependencyRegistrar
{
    public void RegisterAdapter(CommandMetadata metadata, Type adapterType)
    {
        var adapterConstructor = GetSingleConstructor(adapterType);
        var adapterFactory = BuildConstructorDelegate(adapterConstructor);

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            metadata.AdapterDependencyKey,
            adapterFactory
        ).Execute();
    }

    public void RegisterCommand(CommandMetadata metadata)
    {
        var commandFactory = BuildConstructorDelegate(metadata.Constructor);

        Func<object[], object> strategy = args =>
        {
            var adapter = Ioc.Resolve<object>(metadata.AdapterDependencyKey, args[0]);

            return commandFactory(new object[] { adapter });
        };

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            metadata.CommandDependencyKey,
            strategy
        ).Execute();
    }

    private static ConstructorInfo GetSingleConstructor(Type type)
    {
        var constructors = type.GetConstructors();

        return constructors.Length == 1
            ? constructors[0]
            : throw new InvalidOperationException($"Type '{type.FullName}' must have exactly one public constructor, found {constructors.Length}.");
    }

    private static Func<object[], object> BuildConstructorDelegate(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        var argsParameter = Expression.Parameter(typeof(object[]), "args");

        var constructorArguments = parameters
            .Select((parameter, index) => Expression.Convert(
                Expression.ArrayIndex(argsParameter, Expression.Constant(index)),
                parameter.ParameterType))
            .ToArray();

        var newExpression = Expression.New(constructor, constructorArguments);
        var castToObject = Expression.Convert(newExpression, typeof(object));

        var lambda = Expression.Lambda<Func<object[], object>>(castToObject, argsParameter);

        return lambda.Compile();
    }
}
