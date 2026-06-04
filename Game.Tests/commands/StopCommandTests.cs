using Moq;
using Xunit;

namespace Game.Tests
{
    public class StopCommandTests
    {
        [Fact]
        public void Execute_ReplacesInjectedCommandWithEmptyCommand()
        {
            var injectable = new CommandInjectableCommand();
            var mockPreviousCommand = new Mock<ICommand>();
            mockPreviousCommand.Setup(c => c.Execute()).Throws(new InvalidOperationException("Should not be executed"));

            injectable.Inject(mockPreviousCommand.Object);
            var stopCommand = new StopCommand(injectable);

            stopCommand.Execute();

            var exception = Record.Exception(() => injectable.Execute());
            Assert.Null(exception);
            mockPreviousCommand.Verify(c => c.Execute(), Times.Never);
        }

        [Fact]
        public void Execute_ThrowsWhenInjectableIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new StopCommand(default!));
        }
    }
}
