namespace GameServer.Routing;

public sealed class NoWorkersAvailableException : InvalidOperationException
{
    public NoWorkersAvailableException() : base("No worker is available for routing.")
    {
    }
}
