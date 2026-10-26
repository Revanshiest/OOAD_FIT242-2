using Game;
using Game.AdapterGenerator;
using Game.Commands;
using Xunit;

namespace Game.Tests;

public class AdapterSourceBuilderTests
{
    private readonly AdapterSourceBuilder _builder = new();

    [Fact]
    public void BuildAdapterSource_ContainsClassName()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains($"public sealed class {metadata.AdapterClassName}", source);
    }

    [Fact]
    public void BuildAdapterSource_ImplementsInterface()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains(": Game.IMovingObject", source);
    }

    [Fact]
    public void BuildAdapterSource_TakesItemThroughPrimaryConstructor()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains($"public sealed class {metadata.AdapterClassName}(IDictionary<string, object> item) : Game.IMovingObject", source);
        Assert.DoesNotContain("_item", source);
    }

    [Fact]
    public void BuildAdapterSource_GeneratesGetterViaIoCResolve()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains("Ioc.Resolve<Game.Vector>(\"Properties.Position\", item)", source);
        Assert.Contains("Ioc.Resolve<Game.Vector>(\"Properties.Speed\", item)", source);
    }

    [Fact]
    public void BuildAdapterSource_GeneratesSetterViaIoCResolve()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains("Ioc.Resolve<ICommand>(\"Properties.Position.Set\", item, value).Execute()", source);
    }

    [Fact]
    public void BuildAdapterSource_ReadOnlyProperty_HasNoSetter()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.DoesNotContain("Properties.Speed.Set", source);
    }

    [Fact]
    public void BuildAdapterSource_ContainsUsingApp()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains("using App;", source);
    }

    [Fact]
    public void BuildAdapterSource_ForRotatable_GeneratesAngleProperties()
    {
        var constructor = typeof(RotateCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(RotateCommand), typeof(IRotatable), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains("Properties.Angle", source);
        Assert.Contains("Properties.AngleVelocity", source);
    }

    [Fact]
    public void BuildAdapterSource_ForInterfaceWithMethods_GeneratesMethodSignatureAndIoCResolve()
    {
        var constructor = typeof(StopCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(StopCommand), typeof(ICommandInjectable), constructor);

        var source = _builder.BuildAdapterSource(metadata);

        Assert.Contains("void Inject(", source);
        Assert.Contains("Ioc.Resolve<ICommand>(\"ICommandInjectable.Inject\", item, command).Execute();", source);
    }
}
