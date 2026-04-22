using Moq;

public class RotateTest
{
    [Fact]
    public void TestNormalRotation()
    {
        var rotatableMock = new Mock<IRotatable>();
        rotatableMock.SetupProperty(r => r.Angle, new Angle(1));
        rotatableMock.Setup(r => r.AngleVelocity).Returns(new Angle(1));
        var command = new RotateCommand(rotatableMock.Object);
        var expectedAngle = new Angle(2);

        command.Execute();

        Assert.Equal(expectedAngle, rotatableMock.Object.Angle);
    }

    [Fact]
    public void TestCannotReadAngle()
    {
        var rotatableMock = new Mock<IRotatable>();
        rotatableMock.SetupGet(r => r.Angle).Throws(new Exception("Cannot read angle"));
        rotatableMock.Setup(r => r.AngleVelocity).Returns(new Angle(1));

        var command = new RotateCommand(rotatableMock.Object);

        Assert.Throws<Exception>(() => command.Execute());
    }

    [Fact]
    public void TestCannotReadAngleVelocity()
    {
        var rotatableMock = new Mock<IRotatable>();
        rotatableMock.SetupGet(r => r.AngleVelocity).Throws(new Exception("Cannot read velocity"));
        rotatableMock.SetupProperty(r => r.Angle, new Angle(1));

        var command = new RotateCommand(rotatableMock.Object);

        Assert.Throws<Exception>(() => command.Execute());
    }

    [Fact]
    public void TestCannotSetAngle()
    {
        var rotatableMock = new Mock<IRotatable>();
        rotatableMock.Setup(r => r.Angle).Returns(new Angle(1));
        rotatableMock.Setup(r => r.AngleVelocity).Returns(new Angle(1));
        rotatableMock.SetupSet(r => r.Angle = It.IsAny<Angle>()).Throws(new Exception("Cannot set angle"));

        var command = new RotateCommand(rotatableMock.Object);

        Assert.Throws<Exception>(() => command.Execute());
    }
}
