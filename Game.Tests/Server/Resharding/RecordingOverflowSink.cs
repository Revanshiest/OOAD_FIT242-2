using GameServer.Messaging;
using GameServer.Resharding;

namespace Game.Tests.Server.Resharding;

public sealed class RecordingOverflowSink : ITransitOverflowSink
{
    public List<(InboundMessage Message, IMessageQueueWriter<InboundMessage> TargetWriter)> OverflowedMessages { get; } = [];

    public void Overflow(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter)
    {
        OverflowedMessages.Add((message, targetWriter));
    }
}

public sealed class FakeTransitResolver(IReadOnlyDictionary<int, IMessageQueueWriter<InboundMessage>> writersByThreadId) : ITransitResolver
{
    public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId)
    {
        return writersByThreadId[threadId];
    }
}
