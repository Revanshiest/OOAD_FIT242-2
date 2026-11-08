namespace GameServer.Workers;

public interface IGameWorker
{
    bool IsShutdownCompleted { get; }

    bool HasReadyWork { get; }

    WorkerCycleResult ExecuteCycle(DateTimeOffset currentTime);

    bool WaitForIncomingMessage(TimeSpan waitTimeout, CancellationToken cancellationToken);
}

