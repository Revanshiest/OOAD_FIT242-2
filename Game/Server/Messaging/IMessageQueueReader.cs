namespace GameServer.Messaging;

public interface IMessageQueueReader<TMessage>
{
    TMessage ReadBlocking();

    IReadOnlyList<TMessage> DrainAllAvailable();
}
