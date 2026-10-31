using GameServer.Messaging;

namespace Game.Tests.Server.Messaging;

public sealed class RecordingMessageWriter : IMessageQueueWriter<InboundMessage>
{
    public List<InboundMessage> WrittenMessages { get; } = [];

    public bool TryWrite(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        WrittenMessages.Add(message);
        return true;
    }

    public void WriteBlocking(InboundMessage message)
    {
        TryWrite(message);
    }
}
