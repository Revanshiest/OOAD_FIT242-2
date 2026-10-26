using Game;
using Game.Commands;
using Game.IoC;
using Moq;
using Xunit;

namespace Game.Tests;

public class RegisterIoCDependencyMacroCommandTests
{
    public RegisterIoCDependencyMacroCommandTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_Should_Register_MacroCommand_Dependency()
    {
        var mockCommand1 = new Mock<ICommand>().Object;
        var mockCommand2 = new Mock<ICommand>().Object;

        var commandsToExecute = new ICommand[] { mockCommand1, mockCommand2 };

        var registerMacroCmd = new RegisterIoCDependencyMacroCommand();

        registerMacroCmd.Execute();

        var resolvedMacroCommand = Ioc.Resolve<ICommand>("Commands.Macro", new object[] { commandsToExecute });

        Assert.NotNull(resolvedMacroCommand);
        Assert.IsType<MacroCommand>(resolvedMacroCommand);
    }
}
