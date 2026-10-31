namespace GameServer.Workers;

using GameServer.Messaging;

public sealed class CommandRateLimiter : IGameRateLimiter
{
    private readonly double _bucketCapacity;
    private readonly double _tokensRefilledPerSecond;
    private readonly Dictionary<string, TokenBucketState> _bucketsByGameId = new();

    public CommandRateLimiter(double bucketCapacity, double tokensRefilledPerSecond)
    {
        if (bucketCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCapacity), bucketCapacity, "Bucket capacity must allow at least one command.");
        }

        if (tokensRefilledPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokensRefilledPerSecond), tokensRefilledPerSecond, "Token refill rate must not be negative.");
        }

        _bucketCapacity = bucketCapacity;
        _tokensRefilledPerSecond = tokensRefilledPerSecond;
    }

    public bool TryAdmit(string gameId, DateTimeOffset currentTime)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        var currentState = _bucketsByGameId.TryGetValue(gameId, out var existingBucket)
            ? existingBucket
            : new TokenBucketState(_bucketCapacity, currentTime);

        var elapsedSeconds = Math.Max(0, (currentTime - currentState.LastRefillAt).TotalSeconds);
        var availableTokens = Math.Min(
            _bucketCapacity,
            currentState.AvailableTokens + elapsedSeconds * _tokensRefilledPerSecond);

        var commandAdmitted = availableTokens >= 1;
        var remainingTokens = commandAdmitted ? availableTokens - 1 : availableTokens;

        _bucketsByGameId[gameId] = new TokenBucketState(remainingTokens, currentTime);

        return commandAdmitted;
    }

    private sealed record TokenBucketState(double AvailableTokens, DateTimeOffset LastRefillAt);
}
