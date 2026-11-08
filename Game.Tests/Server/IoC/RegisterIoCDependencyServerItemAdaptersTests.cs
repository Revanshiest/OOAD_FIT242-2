namespace Game.Tests.Server.IoC;

using Game;
using Game.Commands;
using Game.IoC;
using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.IoC;

public class RegisterIoCDependencyServerItemAdaptersTests
{
    [Fact]
    public void ServerItemAdapters_AdaptsGameItemToIMovingObject_ViaIoC()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

        new RegisterIoCDependencyServerItemAdapters().Execute();
        new RegisterIoCDependencyMoveCommand().Execute();

        var item = new GameItem();
        item.SetProperty(GameItemProperty.Position, new Vector([100, 200]));
        item.SetProperty(GameItemProperty.Speed, new Vector([-10, 50]));

        var resolvedAdapter = Ioc.Resolve<object>("Adapters.IMovingObject", item);
        Assert.IsAssignableFrom<IMovingObject>(resolvedAdapter);

        var moveCommand = Ioc.Resolve<ICommand>("Commands.Move", item);
        moveCommand.Execute();

        Assert.Equal(new Vector([90, 250]), item.GetProperty(GameItemProperty.Position));
    }
}
