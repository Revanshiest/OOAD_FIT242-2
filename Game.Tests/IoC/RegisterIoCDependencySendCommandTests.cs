namespace Game.Tests;

using App;
using App.Scopes;
using Moq;
using Xunit;

public class RegisterIoCDependencySendCommandTests
{
    public RegisterIoCDependencySendCommandTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }

    [Fact]
    public void Execute_Should_Register_SendCommand_Dependency()
    {
        var mockCommand = new Mock<ICommand>();
        var mockReceiver = new Mock<IMessageReceiver>();
        mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);

        var commandObject = mockCommand.Object;
        var receiverObject = mockReceiver.Object;

        var registerCmd = new RegisterIoCDependencySendCommand();

        registerCmd.Execute();

        var resolvedCommand = Ioc.Resolve<ICommand>(
            "Commands.Send",
            commandObject,
            receiverObject
        );

        Assert.NotNull(resolvedCommand);
        Assert.IsType<SendCommand>(resolvedCommand);
    }
}
