using GameServer.Games;
using GameServer.Games.Pipeline;

namespace Game.Tests.Server.Games.Pipeline;

public class CreateGameItemPipelineTests
{
    [Fact]
    public void Build_ExecutedAsSingleMacro_CreatesItemThroughAllFourPipelineSteps()
    {
        var scope = new Scope();
        var specification = new GameItemCreationSpecification(
            "ship1",
            "Ship",
            new Vector(new[] { 1, 2 }),
            new Vector(new[] { 0, 1 }));

        new CreateGameItemPipeline(scope, specification).Build().Execute();

        var createdItem = scope.GetItem("ship1");
        Assert.Equal("Ship", createdItem.GetProperty(GameItemProperty.DomainType));
        Assert.Equal(new Vector(new[] { 1, 2 }), createdItem.GetProperty(GameItemProperty.Position));
        Assert.Equal(new Vector(new[] { 0, 1 }), createdItem.GetProperty(GameItemProperty.Speed));
    }

    [Fact]
    public void Build_DoesNotMutateScope_BeforeMacroExecution()
    {
        var scope = new Scope();
        var specification = new GameItemCreationSpecification(
            "ship1",
            "Ship",
            new Vector(new[] { 1, 2 }),
            new Vector(new[] { 0, 1 }));

        new CreateGameItemPipeline(scope, specification).Build();

        Assert.False(scope.Contains("ship1"));
    }

    [Fact]
    public void MoveGameItemCommand_AdvancesPositionBySpeed()
    {
        var scope = new Scope();
        var specification = new GameItemCreationSpecification(
            "ship1",
            "Ship",
            new Vector(new[] { 1, 2 }),
            new Vector(new[] { 3, 4 }));
        new CreateGameItemPipeline(scope, specification).Build().Execute();

        new MoveGameItemCommand(scope, "ship1").Execute();

        Assert.Equal(new Vector(new[] { 4, 6 }), scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
    }

    [Fact]
    public void MoveGameItemCommand_ThrowsKeyNotFoundException_WhenItemWasNotCreated()
    {
        var scope = new Scope();

        Assert.Throws<KeyNotFoundException>(() => new MoveGameItemCommand(scope, "missing").Execute());
    }
}
