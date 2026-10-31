namespace GameServer.Messaging;

public interface IGameRateLimiter
{
    bool TryAdmit(string gameId, DateTimeOffset currentTime);
}

public sealed class PermissiveRateLimiter : IGameRateLimiter
{
    public static readonly PermissiveRateLimiter Instance = new();

    private PermissiveRateLimiter()
    {
    }

    public bool TryAdmit(string gameId, DateTimeOffset currentTime)
    {
        return true;
    }
}
