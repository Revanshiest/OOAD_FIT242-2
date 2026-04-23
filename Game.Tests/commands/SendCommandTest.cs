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
}
