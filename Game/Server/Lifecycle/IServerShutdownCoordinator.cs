namespace GameServer.Lifecycle;

public interface IServerShutdownCoordinator
{
    void SealAcceptance();

    ServerShutdownResult SoftStop();

    ServerShutdownResult HardStop();

    ServerShutdownResult Restart();
}

