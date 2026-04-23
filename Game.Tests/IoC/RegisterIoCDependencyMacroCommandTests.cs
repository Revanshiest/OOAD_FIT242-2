using Game;
using Xunit;
using Moq;

public class RegisterIoCDependencyMacroCommandTests
{
    public RegisterIoCDependencyMacroCommandTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
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
