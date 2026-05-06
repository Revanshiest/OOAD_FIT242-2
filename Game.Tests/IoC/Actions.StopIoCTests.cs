using System.Collections.Generic;
using Moq;
using Xunit;

namespace Game.Tests.IoC
{
    public class RegisterIoCDependencyActionsStopTests
    {
        public RegisterIoCDependencyActionsStopTests()
        {
            new InitCommand().Execute();
            var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
        }

        [Fact]
        public void Execute_RegistersActionsStop_ResolvesSuccessfully()
        {
            new RegisterIoCDependencyActionsStop().Execute();
            var mockOrder = new Mock<IDictionary<string, object>>();


            var actionStop = Ioc.Resolve<ICommand>("Actions.Stop", mockOrder.Object);
            actionStop.Execute();

            Assert.NotNull(actionStop);
            Assert.IsType<EmptyCommand>(actionStop);
        }
    }
}
