namespace GameServer.Workers;

public sealed record GameWorkerSettings(
    int CommandQuantumPerGameActivation,
    int MaxQueueLengthPerGame,
    int MaxActiveGames,
    TimeSpan MaxExternalAwaitDuration,
    double RateLimitBucketCapacity,
    double RateLimitTokensPerSecond)
{
    public const int DefaultCommandQuantum = 8;
    public const int DefaultMaxQueueLength = 256;
    public const int DefaultMaxActiveGames = 4096;
    public const int DefaultRateLimitBucketCapacity = 16;
    public const double DefaultRateLimitTokensPerSecond = 64.0;
    public static readonly TimeSpan DefaultMaxExternalAwaitDuration = TimeSpan.FromMinutes(5);

    public static GameWorkerSettings CreateDefault()
    {
        return new GameWorkerSettings(
            DefaultCommandQuantum,
            DefaultMaxQueueLength,
            DefaultMaxActiveGames,
            DefaultMaxExternalAwaitDuration,
            DefaultRateLimitBucketCapacity,
            DefaultRateLimitTokensPerSecond);
    }
}
