using System;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using Game;

public class RegisterIoCDependencyCheckCollisionsTests
{
    public RegisterIoCDependencyCheckCollisionsTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
    }

    [Fact]
    public void Execute_RegistersDetectCollisionDependency()
    {
        new RegisterIoCDependencyCheckCollisions().Execute();

        var left = Substitute.For<ICollisionObject>();
        var right = Substitute.For<ICollisionObject>();

        var cmd = Ioc.Resolve<ICommand>("Commands.DetectCollision", left, right);

        Assert.IsType<DetectCollisionCommand>(cmd);
    }

    [Fact]
    public void Execute_RegistersCheckCollisionsDependency()
    {
        new RegisterIoCDependencyCheckCollisions().Execute();

        var mockObj = Substitute.For<ICollisionObject>();

        Ioc.Resolve<ICommand>("IoC.Register", "Adapters.ICollisionObject",
            (Func<object[], object>)(args => mockObj)).Execute();

        var cmd = Ioc.Resolve<ICommand>("Commands.CheckCollisions", new object());

        Assert.IsType<CheckAllCollisionsCommand>(cmd);
    }
}
