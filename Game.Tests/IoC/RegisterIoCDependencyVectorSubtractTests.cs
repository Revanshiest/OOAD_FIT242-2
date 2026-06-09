using System;
using Xunit;
using Game;

public class RegisterIoCDependencyVectorSubtractTests
{
    public RegisterIoCDependencyVectorSubtractTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
    }

    [Fact]
    public void Execute_RegistersVectorSubtractDependency()
    {
        new RegisterIoCDependencyVectorSubtract().Execute();

        var v1 = new Vector(new int[] { 10, 20, 30 });
        var v2 = new Vector(new int[] { 3, 7, 15 });

        var result = Ioc.Resolve<Vector>("Math.Vector.Subtract", v1, v2);

        Assert.Equal(new Vector(new int[] { 7, 13, 15 }), result);
    }

    [Fact]
    public void Execute_DifferentLengthVectors_ThrowsArgumentException()
    {
        new RegisterIoCDependencyVectorSubtract().Execute();

        var v1 = new Vector(new int[] { 1, 2, 3 });
        var v2 = new Vector(new int[] { 1, 2 });

        var exception = Assert.Throws<ArgumentException>(() => Ioc.Resolve<Vector>("Math.Vector.Subtract", v1, v2));
        Assert.Equal("Векторы должны быть одинаковой размерности.", exception.Message);
    }

    [Fact]
    public void Execute_SameVectors_ReturnsZeroVector()
    {
        new RegisterIoCDependencyVectorSubtract().Execute();

        var v1 = new Vector(new int[] { 5, 10 });
        var v2 = new Vector(new int[] { 5, 10 });

        var result = Ioc.Resolve<Vector>("Math.Vector.Subtract", v1, v2);

        Assert.Equal(new Vector(new int[] { 0, 0 }), result);
    }
}
