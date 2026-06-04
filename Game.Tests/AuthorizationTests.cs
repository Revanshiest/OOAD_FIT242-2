using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using Game;

namespace Game.Tests;

public class AuthorizationTests
{
    public AuthorizationTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }

    [Fact]
    public void CheckAuthorizationCommand_Authorized_ExecutesInnerCommand()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Auth.Check",
            (object[] args) => new EmptyCommand()).Execute();

        var mockInnerCommand = new Mock<ICommand>();

        var authCommand = new CheckAuthorizationCommand("player1", "ship1", mockInnerCommand.Object);
        authCommand.Execute();

        mockInnerCommand.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void CheckAuthorizationCommand_Unauthorized_ThrowsAndDoesNotExecuteInner()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Auth.Check",
            (object[] args) =>
            {
                throw new UnauthorizedAccessException("Not authorized");
                return new EmptyCommand();
            }).Execute();

        var mockInnerCommand = new Mock<ICommand>();

        var authCommand = new CheckAuthorizationCommand("hacker", "ship1", mockInnerCommand.Object);

        Assert.Throws<UnauthorizedAccessException>(() => authCommand.Execute());
        mockInnerCommand.Verify(c => c.Execute(), Times.Never);
    }

    [Fact]
    public void RegisterIoCDependencyAuthorization_AuthorizedPlayer_Succeeds()
    {
        var allowedPlayers = new HashSet<string> { "player1", "player2" };

        Ioc.Resolve<ICommand>("IoC.Register", "Auth.AllowedPlayers.ship1",
            (object[] args) => allowedPlayers).Execute();

        new RegisterIoCDependencyAuthorization().Execute();

        var checkCommand = Ioc.Resolve<ICommand>("Auth.Check", "player1", "ship1");
        checkCommand.Execute();
    }

    [Fact]
    public void RegisterIoCDependencyAuthorization_UnauthorizedPlayer_Throws()
    {
        var allowedPlayers = new HashSet<string> { "player1" };

        Ioc.Resolve<ICommand>("IoC.Register", "Auth.AllowedPlayers.ship1",
            (object[] args) => allowedPlayers).Execute();

        new RegisterIoCDependencyAuthorization().Execute();

        Assert.Throws<UnauthorizedAccessException>(() =>
            Ioc.Resolve<ICommand>("Auth.Check", "enemy", "ship1"));
    }

    [Fact]
    public void GameCommand_UnauthorizedPlayer_ThrowsAndDoesNotExecuteAction()
    {
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 5, 5 }) },
            { "Angle", new Angle(0) }
        };

        var mockRepository = new Mock<IGameObjectRepository>();
        mockRepository.Setup(r => r.Get("ship1")).Returns(gameObject);

        var mockAction = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Shoot",
            (object[] args) => mockAction.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Auth.Check",
            (object[] args) =>
            {
                throw new UnauthorizedAccessException("Not authorized");
                return new EmptyCommand();
            }).Execute();

        var order = new Dictionary<string, object>
        {
            { "PlayerId", "hacker" },
            { "GameObjectId", "ship1" },
            { "CmdType", "Shoot" }
        };

        var gameCommand = new GameCommand(order);

        Assert.Throws<UnauthorizedAccessException>(() => gameCommand.Execute());
        mockAction.Verify(a => a.Execute(), Times.Never);
    }

    [Fact]
    public void Constructor_ThrowsWhenPlayerIdIsNull()
    {
        var mockCommand = new Mock<ICommand>();
        Assert.Throws<ArgumentNullException>(() =>
            new CheckAuthorizationCommand(null!, "ship1", mockCommand.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenInnerCommandIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CheckAuthorizationCommand("player1", "ship1", null!));
    }
}
