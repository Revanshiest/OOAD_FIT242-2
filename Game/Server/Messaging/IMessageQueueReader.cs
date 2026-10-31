namespace GameServer.Messaging;

public interface IMessageQueueReader<TMessage>
{
    TMessage ReadBlocking(CancellationToken cancellationToken);

    IReadOnlyList<TMessage> DrainAllAvailable();
}
