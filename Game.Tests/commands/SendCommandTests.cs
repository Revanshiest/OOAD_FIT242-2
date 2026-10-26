namespace Game.Tests;

using Moq;
using Xunit;

public class SendCommandTests
{
    [Fact]
    public void Execute_CallsReceive_WhenReceiverAcceptsCommand()
    {
        var commandMock = new Mock<ICommand>();
        var receiverMock = new Mock<IMessageReceiver>();

        receiverMock.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);

        var sendCommand = new SendCommand(commandMock.Object, receiverMock.Object);

        sendCommand.Execute();
        receiverMock.Verify(r => r.Receive(commandMock.Object), Times.Once);
    }

    [Fact]
    public void Execute_ThrowsInvalidOperationException_WhenReceiverCannotAcceptCommand()
    {
        var commandMock = new Mock<ICommand>();
        var receiverMock = new Mock<IMessageReceiver>();

        receiverMock.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(false);

        var sendCommand = new SendCommand(commandMock.Object, receiverMock.Object);

        var exception = Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());

        Assert.Equal("Message receiver is busy", exception.Message);
        receiverMock.Verify(r => r.Receive(It.IsAny<ICommand>()), Times.Never);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var receiverMock = new Mock<IMessageReceiver>();

        Assert.Throws<ArgumentNullException>(() => new SendCommand(null!, receiverMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenReceiverIsNull()
    {
        var commandMock = new Mock<ICommand>();

        Assert.Throws<ArgumentNullException>(() => new SendCommand(commandMock.Object, null!));
    }
}
