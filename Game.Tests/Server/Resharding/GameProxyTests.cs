using GameServer.Messaging;
using GameServer.Resharding;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Resharding;

public class GameProxyTests
{
    private readonly RecordingMessageWriter _targetWriter = new();
    private readonly RecordingOverflowSink _overflowSink = new();

    [Fact]
    public void Forward_WritesMessageIntoTargetWriter()
    {
        var gameProxy = new GameProxy("game-1", _targetWriter, _overflowSink);
        var message = new InboundMessage(0, "game-1", "token", "Move", "{}");

        var forwarded = gameProxy.Forward(message);

        Assert.True(forwarded);
        Assert.Single(_targetWriter.WrittenMessages);
        Assert.Empty(_overflowSink.OverflowedMessages);
    }

    [Fact]
    public void Forward_HandsMessageToOverflowSink_WhenTargetWriterIsFull()
    {
        var boundedTarget = new BoundedMessageQueue<InboundMessage>(1);
        var gameProxy = new GameProxy("game-1", boundedTarget, _overflowSink);
        var firstMessage = new InboundMessage(0, "game-1", "token", "Move", "{}");
        var secondMessage = new InboundMessage(0, "game-1", "token", "Move", "{}");

        boundedTarget.TryWrite(firstMessage);
        var forwarded = gameProxy.Forward(secondMessage);

        Assert.True(forwarded);
        Assert.Single(_overflowSink.OverflowedMessages);
        Assert.Same(secondMessage, _overflowSink.OverflowedMessages[0].Message);
        Assert.Same(boundedTarget, _overflowSink.OverflowedMessages[0].TargetWriter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenGameIdIsInvalid(string? gameId)
    {
        Assert.Throws<ArgumentException>(() => new GameProxy(gameId!, _targetWriter, _overflowSink));
    }
}
