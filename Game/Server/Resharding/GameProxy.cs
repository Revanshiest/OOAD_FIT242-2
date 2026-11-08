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
    private readonly DateTimeOffset _createdAt;

    public GameProxy(
        string gameId,
        IMessageQueueWriter<InboundMessage> targetWriter,
        ITransitOverflowSink overflowSink,
        DateTimeOffset createdAt = default)
    {
        _ = !string.IsNullOrWhiteSpace(gameId)
            ? 0
            : throw new ArgumentException("Game ID cannot be null or whitespace.", nameof(gameId));
        ArgumentNullException.ThrowIfNull(targetWriter);
        ArgumentNullException.ThrowIfNull(overflowSink);

        _gameId = gameId;
        _targetWriter = targetWriter;
        _overflowSink = overflowSink;
        _createdAt = createdAt == default ? DateTimeOffset.UtcNow : createdAt;
    }

    public string GameId => _gameId;

    public DateTimeOffset CreatedAt => _createdAt;

    public TimeSpan Age(DateTimeOffset currentTime) => currentTime - _createdAt;

    public bool Forward(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var written = _targetWriter.TryWrite(message);
        _ = !written && Overflow(message);

        return true;
    }

    private bool Overflow(InboundMessage message)
    {
        _overflowSink.Overflow(message, _targetWriter);
        return true;
    }
}
