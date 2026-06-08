using System;
using Xunit;
using NSubstitute;
using Game;

public class DetectCollisionCommandTests
{
    public DetectCollisionCommandTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
    }

    [Fact]
    public void Constructor_LeftObjectNull_ThrowsArgumentNullException()
    {
        var rightObject = Substitute.For<ICollisionObject>();

        var exception = Assert.Throws<ArgumentNullException>(() => new DetectCollisionCommand(null, rightObject));
        Assert.Equal("leftObject", exception.ParamName);
    }

    [Fact]
    public void Constructor_RightObjectNull_ThrowsArgumentNullException()
    {
        var leftObject = Substitute.For<ICollisionObject>();

        var exception = Assert.Throws<ArgumentNullException>(() => new DetectCollisionCommand(leftObject, null));
        Assert.Equal("rightObject", exception.ParamName);
    }

    [Fact]
    public void Execute_PositionVectorsDifferentSize_ThrowsArgumentException()
    {
        var leftObj = Substitute.For<ICollisionObject>();
        leftObj.Position.Returns(new Vector(new int[] { 1, 2, 3 }));
        leftObj.Velocity.Returns(new Vector(new int[] { 1, 1 }));

        var rightObj = Substitute.For<ICollisionObject>();
        rightObj.Position.Returns(new Vector(new int[] { 1, 2 }));
        rightObj.Velocity.Returns(new Vector(new int[] { 1, 1 }));

        var command = new DetectCollisionCommand(leftObj, rightObj);

        var exception = Assert.Throws<ArgumentException>(() => command.Execute());
        Assert.Equal("Векторы должны быть одинаковой размерности.", exception.Message);
    }

    [Fact]
    public void Execute_VelocityVectorsDifferentSize_ThrowsArgumentException()
    {
        var leftObj = Substitute.For<ICollisionObject>();
        leftObj.Position.Returns(new Vector(new int[] { 1, 2 }));
        leftObj.Velocity.Returns(new Vector(new int[] { 1, 1, 1 }));

        var rightObj = Substitute.For<ICollisionObject>();
        rightObj.Position.Returns(new Vector(new int[] { 1, 2 }));
        rightObj.Velocity.Returns(new Vector(new int[] { 1, 1 }));

        var command = new DetectCollisionCommand(leftObj, rightObj);

        var exception = Assert.Throws<ArgumentException>(() => command.Execute());
        Assert.Equal("Векторы должны быть одинаковой размерности.", exception.Message);
    }

    [Fact]
    public void Execute_CollisionDetected_ExecutesEventCommand()
    {
        var leftObj = Substitute.For<ICollisionObject>();
        leftObj.Position.Returns(new Vector(new int[] { 10, 20 }));
        leftObj.Velocity.Returns(new Vector(new int[] { 2, 3 }));

        var rightObj = Substitute.For<ICollisionObject>();
        rightObj.Position.Returns(new Vector(new int[] { 5, 10 }));
        rightObj.Velocity.Returns(new Vector(new int[] { 1, 1 }));

        var mockEventCommand = Substitute.For<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Check",
            (Func<object[], object>)(args => true)).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Event",
            (Func<object[], object>)(args => mockEventCommand)).Execute();

        var command = new DetectCollisionCommand(leftObj, rightObj);
        command.Execute();

        mockEventCommand.Received(1).Execute();
    }

    [Fact]
    public void Execute_NoCollision_DoesNotExecuteEventCommand()
    {
        var leftObj = Substitute.For<ICollisionObject>();
        leftObj.Position.Returns(new Vector(new int[] { 0, 0 }));
        leftObj.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        var rightObj = Substitute.For<ICollisionObject>();
        rightObj.Position.Returns(new Vector(new int[] { 100, 100 }));
        rightObj.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        var mockEventCommand = Substitute.For<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Check",
            (Func<object[], object>)(args => false)).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Event",
            (Func<object[], object>)(args => mockEventCommand)).Execute();

        var command = new DetectCollisionCommand(leftObj, rightObj);
        command.Execute();

        mockEventCommand.DidNotReceive().Execute();
    }
}
