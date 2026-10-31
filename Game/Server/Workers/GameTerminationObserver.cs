namespace GameServer.Workers;

public enum GameTerminationReason
{
    WatchdogAwaitTimeout = 0,
    FloodQueueOverflow = 1
}

public interface IGameTerminationObserver
{
    void NotifyGameTerminated(string gameId, GameTerminationReason terminationReason);
}

public sealed class NullGameTerminationObserver : IGameTerminationObserver
{
    public static readonly NullGameTerminationObserver Instance = new();

    private NullGameTerminationObserver()
    {
    }

    public void NotifyGameTerminated(string gameId, GameTerminationReason terminationReason)
    {
    }
}
