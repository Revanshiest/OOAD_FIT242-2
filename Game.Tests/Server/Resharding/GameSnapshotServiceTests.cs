namespace Game.Tests.Server.Resharding;

using Game;
using Game.Commands;
using GameServer.Games;
using GameServer.Resharding;
using Game = GameServer.Games.Game;

public class GameSnapshotServiceTests
{
    [Fact]
    public void GameSnapshotService_CapturesAndRestoresGameSnapshot_Correctly()
    {
        var service = new GameSnapshotService();
        var scope = new Scope();
        var item = new GameItem();
        item.SetProperty("Position", new Vector([10, 20]));
        item.SetProperty("Angle", new Angle(45));
        item.SetProperty("IsAlive", true);
        scope.Register("ship1", item);

        var game = new Game("game-srp", GamePriority.High, scope);
        game.CommandQueue.Enqueue(new DummyCommand());

        var snapshot = service.CaptureSnapshot(game);

        Assert.Equal("game-srp", snapshot.GameId);
        Assert.Equal(GamePriority.High, snapshot.Priority);
        Assert.Single(snapshot.Items);
        Assert.Single(snapshot.QueuedCommands!);

        var restoredGame = service.RestoreGame(snapshot);

        Assert.Equal("game-srp", restoredGame.GameId);
        Assert.Equal(GamePriority.High, restoredGame.Priority);
        Assert.True(restoredGame.Scope.Contains("ship1"));

        var restoredItem = restoredGame.Scope.GetItem("ship1");
        Assert.Equal(new Vector([10, 20]), restoredItem.GetProperty("Position"));
        Assert.Equal(new Angle(45), restoredItem.GetProperty("Angle"));
        Assert.Equal(true, restoredItem.GetProperty("IsAlive"));
        Assert.Equal(1, restoredGame.CommandQueue.Count);
    }

    [Fact]
    public void GameSnapshotService_QueuedCommands_PreservesCommandParametersAndExecutesAfterRestore()
    {
        var service = new GameSnapshotService();
        var scope = new Scope();
        var item = new GameItem();
        item.SetProperty("Position", new Vector([10, 20]));
        item.SetProperty("Speed", new Vector([5, 5]));
        scope.Register("ship1", item);

        var game = new Game("game-move", GamePriority.Normal, scope);
        game.Tell(new GameServer.Games.Pipeline.MoveGameItemCommand(scope, "ship1"));

        var snapshot = service.CaptureSnapshot(game);
        Assert.NotNull(snapshot.QueuedCommands);
        Assert.Single(snapshot.QueuedCommands);
        Assert.Contains("ship1", snapshot.QueuedCommands[0]);

        var restoredGame = service.RestoreGame(snapshot);
        Assert.Equal(1, restoredGame.CommandQueue.Count);

        restoredGame.ExecuteNextCommand();

        var restoredItem = restoredGame.Scope.GetItem("ship1");
        Assert.Equal(new Vector([15, 25]), restoredItem.GetProperty("Position"));
    }

    [Fact]
    public void GameSnapshotService_QueuedCreateItemCommand_PreservesSpecificationAndCreatesItemAfterRestore()
    {
        var service = new GameSnapshotService();
        var scope = new Scope();
        var game = new Game("game-create", GamePriority.High, scope);
        var spec = new GameServer.Games.Pipeline.GameItemCreationSpecification(
            "tank1",
            "Tank",
            new Vector([100, 200]),
            new Vector([10, -5]));

        game.Tell(new GameServer.Games.Pipeline.CreateGameItemPipeline(scope, spec).Build());

        var snapshot = service.CaptureSnapshot(game);
        Assert.NotNull(snapshot.QueuedCommands);
        Assert.Single(snapshot.QueuedCommands);
        Assert.Contains("tank1", snapshot.QueuedCommands[0]);
        Assert.Contains("Tank", snapshot.QueuedCommands[0]);

        var restoredGame = service.RestoreGame(snapshot);
        Assert.Equal(1, restoredGame.CommandQueue.Count);

        restoredGame.ExecuteNextCommand();

        Assert.True(restoredGame.Scope.Contains("tank1"));
        var tank = restoredGame.Scope.GetItem("tank1");
        Assert.Equal("Tank", tank.GetProperty(GameServer.Games.Pipeline.GameItemProperty.DomainType));
        Assert.Equal(new Vector([100, 200]), tank.GetProperty(GameServer.Games.Pipeline.GameItemProperty.Position));
        Assert.Equal(new Vector([10, -5]), tank.GetProperty(GameServer.Games.Pipeline.GameItemProperty.Speed));
    }

    private sealed class DummyCommand : ICommand
    {
        public void Execute() { }
    }
}

