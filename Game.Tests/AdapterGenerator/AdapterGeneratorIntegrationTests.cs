using Game;
using Game.AdapterGenerator;
using Moq;
using System.Reflection;
using Xunit;

namespace Game.Tests;

public class AdapterGeneratorIntegrationTests
{
    public AdapterGeneratorIntegrationTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }

    [Fact]
    public void GenerateAndRegister_RegistersAdapterForMoveCommand()
    {
        var assemblies = new[] { typeof(MoveCommand).Assembly };
        var scanner = new CommandScanner();
        var sourceBuilder = new AdapterSourceBuilder();
        var compiler = new RoslynAdapterCompiler();
        var registrar = new IoCDependencyRegistrar();

        var generator = new Game.AdapterGenerator.AdapterGenerator(
            scanner, sourceBuilder, compiler, registrar, assemblies);

        generator.GenerateAndRegister();

        var moveMetadata = scanner.Scan(assemblies)
            .First(m => m.CommandType == typeof(MoveCommand));

        var gameObject = new Dictionary<string, object>();
        var adapter = Ioc.Resolve<object>(moveMetadata.AdapterDependencyKey, gameObject);

        Assert.NotNull(adapter);
        Assert.True(typeof(IMovingObject).IsAssignableFrom(adapter.GetType()));
    }

    [Fact]
    public void GenerateAndRegister_RegistersCommandForMoveCommand()
    {
        var assemblies = new[] { typeof(MoveCommand).Assembly };
        var scanner = new CommandScanner();
        var sourceBuilder = new AdapterSourceBuilder();
        var compiler = new RoslynAdapterCompiler();
        var registrar = new IoCDependencyRegistrar();

        var generator = new Game.AdapterGenerator.AdapterGenerator(
            scanner, sourceBuilder, compiler, registrar, assemblies);

        generator.GenerateAndRegister();

        var moveMetadata = scanner.Scan(assemblies)
            .First(m => m.CommandType == typeof(MoveCommand));

        var gameObject = new Dictionary<string, object>();

        var position = new Vector(new int[] { 1, 2 });
        var speed = new Vector(new int[] { 3, 4 });

        Ioc.Resolve<ICommand>("IoC.Register", "Properties.Position",
            (object[] args) => (object)position).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Properties.Speed",
            (object[] args) => (object)speed).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Properties.Position.Set",
            (object[] args) => (object)new EmptyCommand()).Execute();

        var command = Ioc.Resolve<ICommand>(moveMetadata.CommandDependencyKey, gameObject);

        Assert.NotNull(command);
        Assert.IsType<MoveCommand>(command);
    }

    [Fact]
    public void GenerateAndRegister_ViaRegisterCommand_Works()
    {
        var assemblies = new[] { typeof(MoveCommand).Assembly };

        var registerCommand = new RegisterIoCDependencyAdapterGenerator(assemblies);
        registerCommand.Execute();

        var scanner = new CommandScanner();
        var moveMetadata = scanner.Scan(assemblies)
            .First(m => m.CommandType == typeof(MoveCommand));

        var gameObject = new Dictionary<string, object>();
        var adapter = Ioc.Resolve<object>(moveMetadata.AdapterDependencyKey, gameObject);

        Assert.NotNull(adapter);
    }
}
