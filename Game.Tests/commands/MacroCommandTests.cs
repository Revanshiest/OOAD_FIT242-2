using Xunit;
using System;
using Moq;
using Game;

namespace Game.Tests;

public class MacroCommandTests()
{
    [Fact]
    public void MacroCommand_ExecutesAllCommands()
    {
        var cmd1 = new Mock<ICommand>();
        var cmd2 = new Mock<ICommand>();

        var macro = new MacroCommand(new[] { cmd1.Object, cmd2.Object });

        macro.Execute();

        cmd1.Verify(c => c.Execute(), Times.Once);
        cmd2.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void MacroCommand_ThrowsException_AndStopsExecution()
    {
        var cmd1 = new Mock<ICommand>();
        var cmdException = new Mock<ICommand>();
        var cmd2 = new Mock<ICommand>();

        cmdException.Setup(c => c.Execute()).Throws<Exception>();

        var macro = new MacroCommand(new[]
        {
            cmd1.Object,
            cmdException.Object,
            cmd2.Object
        });

        Assert.Throws<Exception>(() => macro.Execute());

        cmd1.Verify(c => c.Execute(), Times.Once);
        cmdException.Verify(c => c.Execute(), Times.Once);
        cmd2.Verify(c => c.Execute(), Times.Never);
    }
}
