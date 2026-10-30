using System.Collections.Generic;
using Game;
using Game.Commands;
using Game.IoC;
using Moq;
using Xunit;

namespace Game.Tests;

public class RegisterIoCDependencyActionsStopTests
{
    public RegisterIoCDependencyActionsStopTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_RegistersActionsStop_ResolvesSuccessfully()
    {
        new RegisterIoCDependencyActionsStop().Execute();
        var gameObject = new Dictionary<string, object>();
        var injectable = new CommandInjectableCommand();
        gameObject["repeatableMove"] = injectable;
        var order = new Dictionary<string, object>
        {
            ["GameObject"] = gameObject,
            ["CmdType"] = "Move"
        };

        var actionStop = Ioc.Resolve<ICommand>("Actions.Stop", order);

        Assert.NotNull(actionStop);
        Assert.IsType<StopCommand>(actionStop);
        actionStop.Execute();

        var exception = Record.Exception(() => injectable.Execute());
        Assert.Null(exception);
    }
}
