using Game;
using Xunit;
using Moq;

public class RegisterIoCDependencyMoveCommandTests
{
    public RegisterIoCDependencyMoveCommandTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }

    [Fact]
    public void Execute_Should_Register_MoveCommand_Dependency()
    {
        var mockGameObject = new Mock<object>().Object;
        var mockMovingObject = new Mock<IMovingObject>().Object;
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Adapters.IMovingObject",
            (object[] _) => mockMovingObject
        ).Execute();

        var registerCmd = new RegisterIoCDependencyMoveCommand();

        registerCmd.Execute();

        var resolvedCommand = Ioc.Resolve<ICommand>("Commands.Move", mockGameObject);

        Assert.NotNull(resolvedCommand);
        Assert.IsType<MoveCommand>(resolvedCommand);
    }
}
