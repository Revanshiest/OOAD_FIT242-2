namespace GameServer.Workers;

using GameServer.Messaging;

public interface IWorkerThreadHost
{
    int ThreadId { get; }

    INonBlockingMessageWriter<InboundMessage> MessageWriter { get; }

    bool IsShutdownCompleted { get; }

    Exception? LastUnhandledException { get; }

    event Action<Exception>? UnhandledExceptionOccurred;

    void Start();

    void Stop();

    void Join();
}

