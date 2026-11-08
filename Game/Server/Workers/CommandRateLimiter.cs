namespace GameServer.Workers;

using GameServer.Messaging;

public sealed class CommandRateLimiter : IGameRateLimiter
{
    private readonly double _bucketCapacity;
    private readonly double _tokensRefilledPerSecond;
    private readonly Dictionary<string, TokenBucketState> _bucketsByGameId = new();
    private readonly object _syncLock = new();

    public CommandRateLimiter(double bucketCapacity, double tokensRefilledPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCapacity, 1, nameof(bucketCapacity));
        ArgumentOutOfRangeException.ThrowIfNegative(tokensRefilledPerSecond, nameof(tokensRefilledPerSecond));

        _bucketCapacity = bucketCapacity;
        _tokensRefilledPerSecond = tokensRefilledPerSecond;
    }

    public bool TryAdmit(string gameId, DateTimeOffset currentTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        lock (_syncLock)
        {
            var currentState = _bucketsByGameId.GetValueOrDefault(gameId, new TokenBucketState(_bucketCapacity, currentTime));

            var elapsedSeconds = Math.Max(0, (currentTime - currentState.LastRefillAt).TotalSeconds);
            var availableTokens = Math.Min(
                _bucketCapacity,
                currentState.AvailableTokens + elapsedSeconds * _tokensRefilledPerSecond);

            var commandAdmitted = availableTokens >= 1;
            var remainingTokens = availableTokens - Convert.ToDouble(commandAdmitted);

            _bucketsByGameId[gameId] = new TokenBucketState(remainingTokens, currentTime);

            return commandAdmitted;
        }
    }

    private sealed record TokenBucketState(double AvailableTokens, DateTimeOffset LastRefillAt);
}
