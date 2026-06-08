using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using Game;

namespace Game.Tests;

public class ShootCommandTests
{
    public ShootCommandTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }

    [Fact]
    public void Execute_CreatesTorpedo_WithCorrectPositionAndSpeed()
    {
        var ship = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 10, 20 }) },
            { "Angle", new Angle(0) }
        };

        var mockRepository = new Mock<IGameObjectRepository>();
        var mockStartCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => mockStartCommand.Object).Execute();

        var shootCommand = new ShootCommand(ship);
        shootCommand.Execute();

        mockRepository.Verify(r => r.Add(
            It.IsAny<string>(),
            It.Is<IDictionary<string, object>>(t =>
                ((Vector)t["Position"]) == new Vector(new int[] { 10, 20 })
            )
        ), Times.Once);
    }

    [Fact]
    public void Execute_AddsTorpedoToRepository()
    {
        var ship = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 5, 5 }) },
            { "Angle", new Angle(2) }
        };

        var mockRepository = new Mock<IGameObjectRepository>();
        var mockStartCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => mockStartCommand.Object).Execute();

        var shootCommand = new ShootCommand(ship);
        shootCommand.Execute();

        mockRepository.Verify(r => r.Add(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()), Times.Once);
    }

    [Fact]
    public void Execute_StartsTorpedoMovement()
    {
        var ship = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 0, 0 }) },
            { "Angle", new Angle(0) }
        };

        var mockRepository = new Mock<IGameObjectRepository>();
        var mockStartCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => mockRepository.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start",
            (object[] args) => mockStartCommand.Object).Execute();

        var shootCommand = new ShootCommand(ship);
        shootCommand.Execute();

        mockStartCommand.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Constructor_ThrowsWhenShipIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ShootCommand(null!));
    }
}
