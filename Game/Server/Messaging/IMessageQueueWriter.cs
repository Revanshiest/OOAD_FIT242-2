namespace GameServer.Messaging;

public interface IMessageQueueWriter<TMessage>
{
    bool TryWrite(TMessage message);

    void WriteBlocking(TMessage message);
}
