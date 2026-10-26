using Game;
using Game.IoC;
using Game.Commands;
using Moq;
using Xunit;

namespace Game.Tests;

public class RegisterIoCDependencyRotateCommandTests
{
    public RegisterIoCDependencyRotateCommandTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_Should_Register_RotateCommand_Dependency()
    {
        var mockGameObject = new Mock<object>().Object;
        var mockRotatableObject = new Mock<IRotatable>().Object;
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Adapters.IRotatable",
            (object[] _) => mockRotatableObject
        ).Execute();

        var registerCmd = new RegisterIoCDependencyRotateCommand();

        registerCmd.Execute();

        var resolvedCommand = Ioc.Resolve<ICommand>("Commands.Rotate", mockGameObject);

        Assert.NotNull(resolvedCommand);
        Assert.IsType<RotateCommand>(resolvedCommand);
    }
}
