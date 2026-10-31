using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.IoC;

namespace Game.Tests.Server.IoC;

public class RegisterIoCDependencyGameItemPipelineTests
{
    [Fact]
    public void Execute_RegistersStrategy_ThatBuildsExecutableCreationPipeline()
    {
        new App.Scopes.InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
        new RegisterIoCDependencyGameItemPipeline().Execute();

        var scope = new Scope();
        var specification = new GameItemCreationSpecification(
            "ship1",
            "Ship",
            new Vector(new[] { 1, 2 }),
            new Vector(new[] { 0, 1 }));

        var pipeline = Ioc.Resolve<ICommand>(RegisterIoCDependencyGameItemPipeline.StrategyKey, scope, specification);

        pipeline.Execute();

        Assert.Equal("Ship", scope.GetItem("ship1").GetProperty(GameItemProperty.DomainType));
        Assert.Equal(new Vector(new[] { 1, 2 }), scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
        Assert.Equal(new Vector(new[] { 0, 1 }), scope.GetItem("ship1").GetProperty(GameItemProperty.Speed));
    }

    [Fact]
    public void StrategyKey_UsesGamesNamespace()
    {
        Assert.Equal("Games.CreateItem", RegisterIoCDependencyGameItemPipeline.StrategyKey);
    }
}
