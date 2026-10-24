using System.Linq.Expressions;
using System.Reflection;

namespace Game.AdapterGenerator;

public class IoCDependencyRegistrar : IDependencyRegistrar
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
            : throw new InvalidOperationException(
                $"Тип '{type.FullName}' должен иметь ровно один публичный конструктор, найдено: {constructors.Length}.");
    }

    private static Func<object[], object> BuildConstructorDelegate(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        var argsParam = Expression.Parameter(typeof(object[]), "args");

        var constructorArgs = parameters
            .Select((param, index) => (Expression)Expression.Convert(
                Expression.ArrayIndex(argsParam, Expression.Constant(index)),
                param.ParameterType))
            .ToArray();

        var newExpression = Expression.New(constructor, constructorArgs);
        var castToObject = Expression.Convert(newExpression, typeof(object));

        var lambda = Expression.Lambda<Func<object[], object>>(castToObject, argsParam);
        return lambda.Compile();
    }
}
