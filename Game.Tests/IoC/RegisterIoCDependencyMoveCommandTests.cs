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

    [Fact]
    public void Execute_Should_Throw_When_No_Arguments_Provided()
    {
        var registerCmd = new RegisterIoCDependencyMoveCommand();
        registerCmd.Execute();

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            Ioc.Resolve<ICommand>("Commands.Move");
        });
    }

    [Fact]
    public void Execute_Should_Create_Command_Even_Without_Registered_Adapter()
    {
        var mockGameObject = new Mock<object>().Object;

        var registerCmd = new RegisterIoCDependencyMoveCommand();
        registerCmd.Execute();

        var command = Ioc.Resolve<ICommand>("Commands.Move", mockGameObject);

        Assert.NotNull(command);
    }

    [Fact]
    public void Execute_Should_Throw_When_Adapter_Is_Invalid_Type()
    {
        var mockGameObject = new Mock<object>().Object;

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Adapters.IMovingObject",
            (object[] _) => new object()
        ).Execute();

        var registerCmd = new RegisterIoCDependencyMoveCommand();
        registerCmd.Execute();

        Assert.Throws<InvalidCastException>(() =>
        {
            Ioc.Resolve<ICommand>("Commands.Move", mockGameObject);
        });
    }

    [Fact]
    public void Execute_Should_Throw_When_Registering_Same_Dependency_Twice()
    {
        var registerCmd = new RegisterIoCDependencyMoveCommand();

        registerCmd.Execute();

        Assert.Throws<ArgumentException>(() =>
        {
            registerCmd.Execute();
        });
    }
}
