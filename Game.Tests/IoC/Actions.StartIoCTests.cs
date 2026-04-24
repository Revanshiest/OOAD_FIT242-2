using System.Collections.Generic;
using Moq;
using Xunit;
public class RegisterIoCDependencyActionsStartTests
{
    public RegisterIoCDependencyActionsStartTests()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro",
            (object[] args) => new Mock<ICommand>().Object).Execute();
    }

    [Fact]
    public void Execute_RegistersActionsStart_ResolvesSuccessfully()
    {
        new RegisterIoCDependencyActionsStart().Execute();
        var mockOrder = new Mock<IDictionary<string, object>>();

        var actionStart = Ioc.Resolve<ICommand>("Actions.Start", mockOrder.Object);

        Assert.NotNull(actionStart);
        Assert.IsAssignableFrom<ICommand>(actionStart);
    }
}
