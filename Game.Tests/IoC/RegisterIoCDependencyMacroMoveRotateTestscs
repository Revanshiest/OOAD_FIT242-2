namespace Game.Tests;

using App;
using App.Scopes;
using Moq;
using Xunit;

public class RegisterIoCDependencyMacroMoveRotateTests
{
    public RegisterIoCDependencyMacroMoveRotateTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro",
            (object[] args) => new MacroCommand((ICommand[])args[0])).Execute();
    }

    [Fact]
    public void Execute_RegistersMacroMove_ResolvesAndExecutesSuccessfully()
    {
        var moveCmd1 = new Mock<ICommand>();
        var moveCmd2 = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Specs.Move",
            (object[] args) => new[] { "Move.Step1", "Move.Step2" }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Move.Step1",
            (object[] args) => moveCmd1.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Move.Step2",
            (object[] args) => moveCmd2.Object).Execute();

        new RegisterIoCDependencyMacroMoveRotate().Execute();

        var macroMove = Ioc.Resolve<ICommand>("Macro.Move");
        macroMove.Execute();

        moveCmd1.Verify(c => c.Execute(), Times.Once);
        moveCmd2.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Execute_RegistersMacroRotate_ResolvesAndExecutesSuccessfully()
    {
        var rotateCmd1 = new Mock<ICommand>();
        var rotateCmd2 = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Specs.Rotate",
            (object[] args) => new[] { "Rotate.Step1", "Rotate.Step2" }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Rotate.Step1",
            (object[] args) => rotateCmd1.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Rotate.Step2",
            (object[] args) => rotateCmd2.Object).Execute();

        new RegisterIoCDependencyMacroMoveRotate().Execute();
        var macroRotate = Ioc.Resolve<ICommand>("Macro.Rotate");
        macroRotate.Execute();

        rotateCmd1.Verify(c => c.Execute(), Times.Once);
        rotateCmd2.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Resolve_Throws_When_SpecificationNotFound()
    {
        new RegisterIoCDependencyMacroMoveRotate().Execute();
        Assert.Throws<Exception>(() => Ioc.Resolve<ICommand>("Macro.Move"));
    }
}
