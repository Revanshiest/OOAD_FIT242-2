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

        [Fact]
        public void Execute_ThrowsException_WhenReceiverCannotAcceptCommand()
        {
            var gameObject = new Dictionary<string, object>();
            var mockBaseCommand = new Mock<ICommand>();
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(false);

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Attack", (object[] args) => mockBaseCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Attack", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var startCommand = new StartCommand(gameObject, "Attack");

            Assert.Throws<InvalidOperationException>(() => startCommand.Execute());
        }

        [Fact]
        public void Execute_ThrowsException_WhenReceiverThrowsOnReceive()
        {
            var gameObject = new Dictionary<string, object>();
            var mockBaseCommand = new Mock<ICommand>();
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);
            mockReceiver.Setup(r => r.Receive(It.IsAny<ICommand>()))
                .Throws(new InvalidOperationException("Receiver error"));

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Shoot", (object[] args) => mockBaseCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Shoot", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var startCommand = new StartCommand(gameObject, "Shoot");

            Assert.Throws<InvalidOperationException>(() => startCommand.Execute());
        }

        [Fact]
        public void Execute_ThrowsException_WhenCommandExecutionFails()
        {
            var gameObject = new Dictionary<string, object>();
            var mockBaseCommand = new Mock<ICommand>();
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockRepeatableCommand.Setup(c => c.Execute()).Throws(new ArgumentException("Command error"));
            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);
            mockReceiver.Setup(r => r.Receive(It.IsAny<ICommand>()))
                .Callback<ICommand>(c => c.Execute());

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Rotate", (object[] args) => mockBaseCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Rotate", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var startCommand = new StartCommand(gameObject, "Rotate");

            Assert.Throws<ArgumentException>(() => startCommand.Execute());
        }

        [Fact]
        public void Execute_WithEmptyGameObject_StoresInjectableSuccessfully()
        {
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);
            mockReceiver.Setup(r => r.Receive(It.IsAny<ICommand>()))
                .Callback<ICommand>(c => c.Execute());

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Defend", (object[] args) => new Mock<ICommand>().Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Defend", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var gameObject = new Dictionary<string, object>();
            var startCommand = new StartCommand(gameObject, "Defend");

            startCommand.Execute();

            Assert.True(gameObject.TryGetValue("repeatableDefend", out var stored));
            Assert.NotNull(stored);
        }

        [Fact]
        public void Execute_StoredInjectableIsExecutable()
        {
            var gameObject = new Dictionary<string, object>();
            var mockBaseCommand = new Mock<ICommand>();
            var mockRepeatableCommand = new Mock<ICommand>();
            var mockReceiver = new Mock<IMessageReceiver>();

            mockReceiver.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);
            mockReceiver.Setup(r => r.Receive(It.IsAny<ICommand>()))
                .Callback<ICommand>(c => c.Execute());

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Heal", (object[] args) => mockBaseCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Macro.Heal", (object[] args) => mockRepeatableCommand.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandsReceiver", (object[] args) => mockReceiver.Object).Execute();

            var startCommand = new StartCommand(gameObject, "Heal");
            startCommand.Execute();

            var stored = (ICommand)gameObject["repeatableHeal"];
            var newCommand = new Mock<ICommand>();
            ((ICommandInjectable)stored).Inject(newCommand.Object);
            stored.Execute();

            newCommand.Verify(c => c.Execute(), Times.Once);
        }
    }
}
