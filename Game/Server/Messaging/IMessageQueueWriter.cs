namespace GameServer.Messaging;

public interface INonBlockingMessageWriter<in TMessage>
{
    bool TryWrite(TMessage message);
}

public interface IBlockingMessageWriter<in TMessage>
{
    void WriteBlocking(TMessage message);
}

public interface IMessageQueueWriter<TMessage> : INonBlockingMessageWriter<TMessage>, IBlockingMessageWriter<TMessage>
{
}
