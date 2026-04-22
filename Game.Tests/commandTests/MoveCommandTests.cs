namespace Game.Tests;

using Xunit;
using System;
using Moq;

public class MoveCommandTests()
{
    [Fact]
    public void GoodExecute_MovesObjectCorrectly()
    {
        var initialPosition = new Vector(new int[] { 12, 5 });
        var velocity = new Vector(new int[] { -4, 1 });
        var expectedPosition = new Vector(new int[] { 8, 6 });

        var mockMovingObject = new Mock<IMovingObject>();
        mockMovingObject.SetupGet(x => x.Position).Returns(initialPosition);
        mockMovingObject.SetupGet(x => x.Speed).Returns(velocity);

        var cmd = new MoveCommand(mockMovingObject.Object);

        cmd.Execute();

        mockMovingObject.VerifySet(x => x.Position = It.IsAny<Vector>(), Times.Once);
        var actualPosition = mockMovingObject.Object.Position + velocity;
        Assert.Equal(expectedPosition, actualPosition);
    }

    [Fact]
    public void CantGetPosition_ThrowsException()
    {
        var mockMovingObject = new Mock<IMovingObject>();
        mockMovingObject
            .SetupGet(x => x.Position)
            .Throws(new InvalidOperationException("Cannot determine position"));
        mockMovingObject.SetupGet(x => x.Speed).Returns(new Vector(new int[] { 1, 1 }));

        var cmd = new MoveCommand(mockMovingObject.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void CantGetSpeed_ThrowsException()
    {
        var mockMovingObject = new Mock<IMovingObject>();
        mockMovingObject.SetupGet(x => x.Position).Returns(new Vector(new int[] { 5, 5 }));
        mockMovingObject
            .SetupGet(x => x.Speed)
            .Throws(new InvalidOperationException("Cannot determine speed"));

        var cmd = new MoveCommand(mockMovingObject.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void CantSetPosition_ThrowsException()
    {
        var initialPosition = new Vector(new int[] { 5, 5 });
        var velocity = new Vector(new int[] { 1, 1 });

        var mockMovingObject = new Mock<IMovingObject>();
        mockMovingObject.SetupGet(x => x.Position).Returns(initialPosition);
        mockMovingObject.SetupGet(x => x.Speed).Returns(velocity);
        mockMovingObject
            .SetupSet(x => x.Position = It.IsAny<Vector>())
            .Throws(new InvalidOperationException("Cannot change position"));

        var cmd = new MoveCommand(mockMovingObject.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }
}
