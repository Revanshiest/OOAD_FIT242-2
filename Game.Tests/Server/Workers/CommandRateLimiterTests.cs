namespace Game.Tests.Server.Workers;

using GameServer.Workers;

public class CommandRateLimiterTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenBucketCapacityBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandRateLimiter(0, 1));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenRefillRateIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandRateLimiter(4, -0.1));
    }

    [Fact]
    public void TryAdmit_ReturnsTrue_WhileBucketHasTokens()
    {
        var rateLimiter = new CommandRateLimiter(3, 0);

        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp));
        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp));
        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp));
    }

    [Fact]
    public void TryAdmit_ReturnsFalse_AfterBucketIsExhausted()
    {
        var rateLimiter = new CommandRateLimiter(2, 0);
        Enumerable.Range(0, 2).ToList().ForEach(_ => rateLimiter.TryAdmit("game-1", TestTimestamp));

        var admittedAfterExhaustion = rateLimiter.TryAdmit("game-1", TestTimestamp);

        Assert.False(admittedAfterExhaustion);
    }

    [Fact]
    public void TryAdmit_RefillsTokens_AfterElapsedTime()
    {
        var rateLimiter = new CommandRateLimiter(2, 1);
        Enumerable.Range(0, 2).ToList().ForEach(_ => rateLimiter.TryAdmit("game-1", TestTimestamp));
        Assert.False(rateLimiter.TryAdmit("game-1", TestTimestamp));

        var admittedAfterOneSecond = rateLimiter.TryAdmit("game-1", TestTimestamp.AddSeconds(1));

        Assert.True(admittedAfterOneSecond);
        Assert.False(rateLimiter.TryAdmit("game-1", TestTimestamp.AddSeconds(1)));
    }

    [Fact]
    public void TryAdmit_NeverExceedsBucketCapacity_AfterLongIdlePeriod()
    {
        var rateLimiter = new CommandRateLimiter(2, 100);
        rateLimiter.TryAdmit("game-1", TestTimestamp);

        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp.AddHours(1)));
        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp.AddHours(1)));
        Assert.False(rateLimiter.TryAdmit("game-1", TestTimestamp.AddHours(1)));
    }

    [Fact]
    public void TryAdmit_DoesNotRefill_WhenObservedTimeGoesBackwards()
    {
        var rateLimiter = new CommandRateLimiter(1, 100);
        rateLimiter.TryAdmit("game-1", TestTimestamp);

        var admittedAfterClockSkew = rateLimiter.TryAdmit("game-1", TestTimestamp.AddSeconds(-30));

        Assert.False(admittedAfterClockSkew);
    }

    [Fact]
    public void TryAdmit_TreatsEveryGameAsIndependentBucket()
    {
        var rateLimiter = new CommandRateLimiter(1, 0);

        Assert.True(rateLimiter.TryAdmit("game-1", TestTimestamp));
        Assert.False(rateLimiter.TryAdmit("game-1", TestTimestamp));
        Assert.True(rateLimiter.TryAdmit("game-2", TestTimestamp));
    }

    [Fact]
    public void TryAdmit_ThrowsArgumentException_WhenGameIdIsInvalid()
    {
        var rateLimiter = new CommandRateLimiter(1, 0);

        Assert.Throws<ArgumentException>(() => rateLimiter.TryAdmit("  ", TestTimestamp));
    }
}
