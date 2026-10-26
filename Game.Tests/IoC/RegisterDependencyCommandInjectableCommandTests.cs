using Game;
using Game.Commands;
using Game.IoC;
using Xunit;

namespace Game.Tests;

public class RegisterDependencyCommandInjectableCommandTests
{
    public RegisterDependencyCommandInjectableCommandTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_RegistersDependency_ResolvesAsAllRequiredTypes()
    {
        new RegisterDependencyCommandInjectableCommand().Execute();

        var asCommand = Ioc.Resolve<ICommand>("Commands.CommandInjectable");
        var asInjectable = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");
        var asConcrete = Ioc.Resolve<CommandInjectableCommand>("Commands.CommandInjectable");

        Assert.IsType<CommandInjectableCommand>(asCommand);
        Assert.IsType<CommandInjectableCommand>(asInjectable);
        Assert.IsType<CommandInjectableCommand>(asConcrete);
    }
}
