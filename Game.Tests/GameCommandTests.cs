using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using Game;

namespace Game.Tests;

public class GameCommandTests
{
    public GameCommandTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Auth.Check",
            (object[] args) => new EmptyCommand()).Execute();
    }

    [Fact]
    public void Execute_ResolvesGameObjectAndExecutesAction()
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

        var order = new Dictionary<string, object>
        {
            { "PlayerId", "player1" },
            { "GameObjectId", "ship1" },
            { "CmdType", "Shoot" }
        };

        var gameCommand = new GameCommand(order);
        gameCommand.Execute();

        mockRepository.Verify(r => r.Get("ship1"), Times.Once);
        mockAction.Verify(a => a.Execute(), Times.Once);
    }

    [Fact]
    public void Execute_WithMoveCommand_DelegatesToStart()
    {
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 0, 0 }) },
            { "Speed", new Vector(new int[] { 1, 1 }) }
        };

        var mockRepository = new Mock<IGameObjectRepository>();
        mockRepository.Setup(r => r.Get("ship2")).Returns(gameObject);

        var mockAction = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => mockAction.Object).Execute();

        var order = new Dictionary<string, object>
        {
            { "PlayerId", "player1" },
            { "GameObjectId", "ship2" },
            { "CmdType", "Start" }
        };

        var gameCommand = new GameCommand(order);
        gameCommand.Execute();

        mockAction.Verify(a => a.Execute(), Times.Once);
    }

    [Fact]
    public void Execute_ThrowsWhenGameObjectNotFound()
    {
        var mockRepository = new Mock<IGameObjectRepository>();
        mockRepository.Setup(r => r.Get("unknown"))
            .Throws(new KeyNotFoundException("Game object with id 'unknown' not found"));

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();

        var order = new Dictionary<string, object>
        {
            { "PlayerId", "player1" },
            { "GameObjectId", "unknown" },
            { "CmdType", "Shoot" }
        };

        var gameCommand = new GameCommand(order);

        Assert.Throws<KeyNotFoundException>(() => gameCommand.Execute());
    }

    [Fact]
    public void Constructor_ThrowsWhenOrderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new GameCommand(null!));
    }
}
