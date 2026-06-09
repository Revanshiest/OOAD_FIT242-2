using System;
using System.Collections.Generic;
using Xunit;
using Game;

public class RegisterIoCDependencyCollisionCheckTests
{
    public RegisterIoCDependencyCollisionCheckTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

        new RegisterIoCDependencyVectorSubtract().Execute();
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyCollisionCheck(null!));
    }

    [Fact]
    public void Execute_RegistersDependency_CollisionFound()
    {
        var data = new HashSet<(int, int, int, int)> { (5, 10, 1, 2) };
        new RegisterIoCDependencyCollisionCheck(data).Execute();

        var deltaPos = new Vector(new int[] { 5, 10 });
        var deltaVel = new Vector(new int[] { 1, 2 });

        var result = Ioc.Resolve<bool>("Game.Collision.Check", deltaPos, deltaVel);

        Assert.True(result);
    }

    [Fact]
    public void Execute_RegistersDependency_NoCollision()
    {
        var data = new HashSet<(int, int, int, int)> { (5, 10, 1, 2) };
        new RegisterIoCDependencyCollisionCheck(data).Execute();

        var deltaPos = new Vector(new int[] { 99, 99 });
        var deltaVel = new Vector(new int[] { 0, 0 });

        var result = Ioc.Resolve<bool>("Game.Collision.Check", deltaPos, deltaVel);

        Assert.False(result);
    }
}
