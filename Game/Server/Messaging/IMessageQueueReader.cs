namespace GameServer.Messaging;

public interface IMessageQueueReader<TMessage>
{
    bool WaitForMessage(TimeSpan waitTimeout, CancellationToken cancellationToken);

    TMessage ReadBlocking(CancellationToken cancellationToken);

    IReadOnlyList<TMessage> DrainAllAvailable();
}
