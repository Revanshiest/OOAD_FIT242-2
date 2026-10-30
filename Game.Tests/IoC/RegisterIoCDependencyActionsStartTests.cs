using System.Collections.Generic;
using Game.IoC;
using Moq;
using Xunit;

namespace Game.Tests;

public class RegisterIoCDependencyActionsStartTests
{
    public RegisterIoCDependencyActionsStartTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_RegistersActionsStart_ResolvesSuccessfully()
    {
        new RegisterIoCDependencyActionsStart().Execute();
        var mockOrder = new Mock<IDictionary<string, object>>();

        var actionStart = Ioc.Resolve<ICommand>("Actions.Start", mockOrder.Object, "Move");

        Assert.NotNull(actionStart);
        Assert.IsAssignableFrom<ICommand>(actionStart);
    }
}
