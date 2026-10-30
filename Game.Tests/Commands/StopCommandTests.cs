using Game;
using Game.Commands;
using Moq;
using Xunit;

namespace Game.Tests;

public class StopCommandTests
{
    [Fact]
    public void Execute_ReplacesInjectedCommandWithEmptyCommand()
    {
        var injectable = new CommandInjectableCommand();
        var previousCommandMock = new Mock<ICommand>();
        previousCommandMock.Setup(c => c.Execute()).Throws(new InvalidOperationException("Should not be executed"));

        injectable.Inject(previousCommandMock.Object);
        var stopCommand = new StopCommand(injectable);

        stopCommand.Execute();

        var exception = Record.Exception(() => injectable.Execute());
        Assert.Null(exception);
        previousCommandMock.Verify(c => c.Execute(), Times.Never);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenInjectableIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StopCommand(null!));
    }
}
