using Moq;
using Xunit;

namespace Game.Tests
{
    public class CommandInjectableCommandTests
    {
        [Fact]
        public void Execute_InvokesInjectedCommand()
        {
            // Arrange
            var mockCommand = new Mock<ICommand>();
            var injectable = new CommandInjectableCommand();

            // Act: Внедряем команду и выполняем
            injectable.Inject(mockCommand.Object);
            injectable.Execute();

            // Assert: Проверяем, что внедрённая команда была вызвана ровно 1 раз
            mockCommand.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void Execute_ThrowsException_WhenCommandNotInjected()
        {
            // Arrange
            var injectable = new CommandInjectableCommand();

            // Act & Assert: Execute без Inject должен выбросить исключение
            var ex = Assert.Throws<NullReferenceException>(() => injectable.Execute());
        }
    }
}
