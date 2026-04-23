namespace Game.Tests;

using Moq;
using Xunit;

public class SendCommandTests
{
    [Fact]
    public void SendCommand_Should_Call_Receive_On_Success()
    {
        var commandMock = new Mock<ICommand>();

        var receiverMock = new Mock<IMessageReceiver>();

        receiverMock.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(true);

        var sendCommand = new SendCommand(commandMock.Object, receiverMock.Object);

        sendCommand.Execute();
        receiverMock.Verify(r => r.Receive(commandMock.Object), Times.Once);
    }

    [Fact]
    public void SendCommand_Should_Throw_Exception_If_Receiver_Cannot_Accept()
    {
        var commandMock = new Mock<ICommand>();
        var receiverMock = new Mock<IMessageReceiver>();

        receiverMock.Setup(r => r.CanAccept(It.IsAny<ICommand>())).Returns(false);

        var sendCommand = new SendCommand(commandMock.Object, receiverMock.Object);

        var exception = Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());

        Assert.Equal("recipient is busy", exception.Message);
        receiverMock.Verify(r => r.Receive(It.IsAny<ICommand>()), Times.Never);
    }

    [Fact]
    public void SendCommand_Constructor_Throws_When_Command_Is_Null()
    {
        var receiverMock = new Mock<IMessageReceiver>();
        Assert.Throws<ArgumentNullException>(() => new SendCommand(null, receiverMock.Object));
    }

    [Fact]
    public void SendCommand_Constructor_Throws_When_Receiver_Is_Null()
    {
        var commandMock = new Mock<ICommand>();
        Assert.Throws<ArgumentNullException>(() => new SendCommand(commandMock.Object, null));
    }
}
