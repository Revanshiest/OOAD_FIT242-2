using System.Collections.Generic;
using Moq;
using Xunit;

namespace Game.Tests
{
    public class StartCommandTests
    {
        public StartCommandTests()
        {
            new InitCommand().Execute();
            var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
            new RegisterDependencyCommandInjectableCommand().Execute();
        }

        [Fact]
        public void Execute_StartsCommand_UsesInjectableAndSendsToReceiver()
        {
            var gameObject = new Dictionary<string, object>();
            var mockBaseCommand = new Mock<ICommand>();
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);
            mockReceiver.Setup(r => r.Receive(It.IsAny<ICommand>()))
                .Callback<ICommand>(c => c.Execute());

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move", (object[] args) => mockBaseCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Move", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var startCommand = new StartCommand(gameObject, "Move");

            startCommand.Execute();

            mockReceiver.Verify(r => r.CanAccept(It.IsAny<ICommand>()), Times.Once);
            mockReceiver.Verify(r => r.Receive(It.IsAny<ICommand>()), Times.Once);
            mockRepeatableCommand.Verify(c => c.Execute(), Times.Once);
            Assert.True(gameObject.TryGetValue("repeatableMove", out var stored));
            Assert.NotNull(stored);
            Assert.IsAssignableFrom<ICommandInjectable>(stored);
        }
    }
}
