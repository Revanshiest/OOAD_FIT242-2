namespace Game.Tests.Server.Games.Pipeline;

using Game;
using Game.Commands;
using Game.IoC;
using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.IoC;

public class MoveGameItemCommandTests
{
    [Fact]
    public void MoveGameItemCommand_UsesMovingObjectAdapter_AndDomainMoveCommand()
    {
        var scope = new Scope();
        var item = new GameItem();
        item.SetProperty(GameItemProperty.Position, new Vector([5, 10]));
        item.SetProperty(GameItemProperty.Speed, new Vector([2, 3]));
        scope.Register("ship-move", item);

        var moveCommand = new MoveGameItemCommand(scope, "ship-move");
        moveCommand.Execute();

        Assert.Equal(new Vector([7, 13]), item.GetProperty(GameItemProperty.Position));
    }

    [Fact]
    public void MoveGameItemCommand_ResolvesIMovingObject_FromIoC()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

        var scope = new Scope();
        var item = new GameItem();
        item.SetProperty("Position", new Vector([5, 5]));
        item.SetProperty("Speed", new Vector([2, 3]));
        scope.Register("ship-move", item);

        new RegisterIoCDependencyServerItemAdapters().Execute();

        var moveCmd = new MoveGameItemCommand(scope, "ship-move");
        moveCmd.Execute();

        Assert.Equal(new Vector([7, 8]), item.GetProperty("Position"));
    }
}

