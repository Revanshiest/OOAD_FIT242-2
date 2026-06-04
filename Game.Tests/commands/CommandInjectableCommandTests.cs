using Moq;
using Xunit;

namespace Game.Tests
{
    public class CommandInjectableCommandTests
    {
        [Fact]
        public void Execute_InvokesInjectedCommand()
        {
            var mockCommand = new Mock<ICommand>();
            var injectable = new CommandInjectableCommand();

            injectable.Inject(mockCommand.Object);
            injectable.Execute();

            mockCommand.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void Execute_ThrowsException_WhenCommandNotInjected()
        {
            var injectable = new CommandInjectableCommand();
            var ex = Assert.Throws<InvalidOperationException>(() => injectable.Execute());
            Assert.Equal("Command not injected", ex.Message);
        }
        [Fact]
        public void Inject_Throws_When_Command_Is_Null()
        {
            var injectable = new CommandInjectableCommand();

            Assert.Throws<ArgumentNullException>(() =>
                injectable.Inject(null!)
            );
        }
    }
}
