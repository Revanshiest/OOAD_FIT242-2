using Game;
using Game.Commands;
using Moq;
using Xunit;

namespace Game.Tests;

public class CommandInjectableCommandTests
{
    [Fact]
    public void Execute_InvokesInjectedCommand()
    {
        var commandMock = new Mock<ICommand>();
        var injectable = new CommandInjectableCommand();

        injectable.Inject(commandMock.Object);
        injectable.Execute();

        commandMock.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Execute_ThrowsInvalidOperationException_WhenCommandIsNotInjected()
    {
        var injectable = new CommandInjectableCommand();

        var exception = Assert.Throws<InvalidOperationException>(() => injectable.Execute());

        Assert.Equal("Command not injected", exception.Message);
    }

    [Fact]
    public void Inject_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var injectable = new CommandInjectableCommand();

        Assert.Throws<ArgumentNullException>(() => injectable.Inject(null!));
    }
}
