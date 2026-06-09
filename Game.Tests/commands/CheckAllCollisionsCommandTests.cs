using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using Game;

public class CheckAllCollisionsCommandTests
{
    public CheckAllCollisionsCommandTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

        new RegisterIoCDependencyVectorSubtract().Execute();
    }

    [Fact]
    public void Constructor_NullObject_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CheckAllCollisionsCommand(null!));
    }

    [Fact]
    public void Execute_NoOtherObjects_NoCollisionChecks()
    {
        var current = Substitute.For<ICollisionObject>();
        current.Position.Returns(new Vector(new int[] { 0, 0 }));
        current.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Collidable",
            (Func<object[], object>)(args => new List<ICollisionObject> { current }.AsEnumerable())).Execute();

        var detectCalled = false;
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.DetectCollision",
            (Func<object[], object>)(args =>
            {
                detectCalled = true;
                return Substitute.For<ICommand>();
            })).Execute();

        var command = new CheckAllCollisionsCommand(current);
        command.Execute();

        Assert.False(detectCalled);
    }

    [Fact]
    public void Execute_MultipleObjects_ChecksCollisionWithEach()
    {
        var current = Substitute.For<ICollisionObject>();
        current.Position.Returns(new Vector(new int[] { 0, 0 }));
        current.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        var other1 = Substitute.For<ICollisionObject>();
        other1.Position.Returns(new Vector(new int[] { 1, 1 }));
        other1.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        var other2 = Substitute.For<ICollisionObject>();
        other2.Position.Returns(new Vector(new int[] { 2, 2 }));
        other2.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Collidable",
            (Func<object[], object>)(args => new List<ICollisionObject> { current, other1, other2 }.AsEnumerable())).Execute();

        var detectCount = 0;
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.DetectCollision",
            (Func<object[], object>)(args =>
            {
                detectCount++;
                var mockCmd = Substitute.For<ICommand>();
                return mockCmd;
            })).Execute();

        var command = new CheckAllCollisionsCommand(current);
        command.Execute();

        Assert.Equal(2, detectCount);
    }

    [Fact]
    public void Execute_DetectCollisionThrows_ExceptionPropagates()
    {
        var current = Substitute.For<ICollisionObject>();
        current.Position.Returns(new Vector(new int[] { 0, 0 }));
        current.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        var other = Substitute.For<ICollisionObject>();
        other.Position.Returns(new Vector(new int[] { 1, 1 }));
        other.Velocity.Returns(new Vector(new int[] { 0, 0 }));

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Collidable",
            (Func<object[], object>)(args => new List<ICollisionObject> { current, other }.AsEnumerable())).Execute();

        var failingCmd = Substitute.For<ICommand>();
        failingCmd.When(c => c.Execute()).Do(_ => throw new InvalidOperationException("Collision error"));

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.DetectCollision",
            (Func<object[], object>)(args => failingCmd)).Execute();

        var command = new CheckAllCollisionsCommand(current);

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void Execute_ObjectsResolveThrows_ExceptionPropagates()
    {
        var current = Substitute.For<ICollisionObject>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Collidable",
            (Func<object[], object>)(args => throw new InvalidOperationException("Not registered"))).Execute();

        var command = new CheckAllCollisionsCommand(current);

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }
}
