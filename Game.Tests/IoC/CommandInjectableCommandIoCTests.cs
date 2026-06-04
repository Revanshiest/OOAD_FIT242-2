using Xunit;

namespace Game.Tests.IoC
{
    public class RegisterDependencyCommandInjectableCommandTests
    {
        public RegisterDependencyCommandInjectableCommandTests()
        {
            new InitCommand().Execute();
            var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
        }

        [Fact]
        public void Execute_RegistersDependency_ResolvesAsAllRequiredTypes()
        {
            new RegisterDependencyCommandInjectableCommand().Execute();

            var asCommand = Ioc.Resolve<ICommand>("Commands.CommandInjectable");
            var asInjectable = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");
            var asConcrete = Ioc.Resolve<CommandInjectableCommand>("Commands.CommandInjectable");
        }
    }
}
