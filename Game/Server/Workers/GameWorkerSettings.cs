namespace GameServer.Workers;

public sealed class GameWorkerSettings
{
    public const int DefaultCommandQuantum = 8;
    public const int DefaultMaxQueueLength = 256;
    public const int DefaultMaxActiveGames = 4096;
    public const int DefaultRateLimitBucketCapacity = 16;
    public const double DefaultRateLimitTokensPerSecond = 64.0;
    public static readonly TimeSpan DefaultMaxExternalAwaitDuration = TimeSpan.FromMinutes(5);

    public GameWorkerSettings(
        int commandQuantumPerGameActivation,
        int maxQueueLengthPerGame,
        int maxActiveGames,
        TimeSpan maxExternalAwaitDuration,
        double rateLimitBucketCapacity,
        double rateLimitTokensPerSecond)
    {
        ValidateMinimum(commandQuantumPerGameActivation, 1, nameof(commandQuantumPerGameActivation));
        ValidateMinimum(maxQueueLengthPerGame, 1, nameof(maxQueueLengthPerGame));
        ValidateMinimum(maxActiveGames, 1, nameof(maxActiveGames));
        ValidateMinimum(rateLimitBucketCapacity, 1, nameof(rateLimitBucketCapacity));
        ValidateMinimum(rateLimitTokensPerSecond, 0, nameof(rateLimitTokensPerSecond));

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            maxExternalAwaitDuration,
            TimeSpan.Zero,
            nameof(maxExternalAwaitDuration));

        CommandQuantumPerGameActivation = commandQuantumPerGameActivation;
        MaxQueueLengthPerGame = maxQueueLengthPerGame;
        MaxActiveGames = maxActiveGames;
        MaxExternalAwaitDuration = maxExternalAwaitDuration;
        RateLimitBucketCapacity = rateLimitBucketCapacity;
        RateLimitTokensPerSecond = rateLimitTokensPerSecond;
    }

    public int CommandQuantumPerGameActivation { get; }

    public int MaxQueueLengthPerGame { get; }

    public int MaxActiveGames { get; }

    public TimeSpan MaxExternalAwaitDuration { get; }

    public double RateLimitBucketCapacity { get; }

    public double RateLimitTokensPerSecond { get; }

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

    private static void ValidateMinimum(double fieldValue, double minimumValue, string fieldName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldValue, minimumValue, fieldName);
    }
}
