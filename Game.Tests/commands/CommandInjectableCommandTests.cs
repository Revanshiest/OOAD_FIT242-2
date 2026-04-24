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
            var ex = Assert.Throws<NullReferenceException>(() => injectable.Execute());
        }
    }
}
