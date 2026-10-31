namespace GameServer.Resharding;

using GameServer.Messaging;

public interface ITransitOverflowSink
{
    void Overflow(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter);
}

public interface ITransitResolver
{
    IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId);
}

public sealed record TransitEnvelope(InboundMessage Message, IMessageQueueWriter<InboundMessage> TargetWriter);

public sealed class GameProxy : IGameRecipient
{
    private readonly string _gameId;
    private readonly IMessageQueueWriter<InboundMessage> _targetWriter;
    private readonly ITransitOverflowSink _overflowSink;

    public GameProxy(string gameId, IMessageQueueWriter<InboundMessage> targetWriter, ITransitOverflowSink overflowSink)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        _gameId = gameId;
        _targetWriter = targetWriter ?? throw new ArgumentNullException(nameof(targetWriter));
        _overflowSink = overflowSink ?? throw new ArgumentNullException(nameof(overflowSink));
    }

    public string GameId => _gameId;

    public bool Forward(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_targetWriter.TryWrite(message))
        {
            return true;
        }

        _overflowSink.Overflow(message, _targetWriter);
        return true;
    }
}
