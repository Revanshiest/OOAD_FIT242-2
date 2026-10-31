namespace Game.Tests.Server.Workers;

using GameServer.Workers;

public class GameWorkerSettingsTests
{
    [Fact]
    public void CreateDefault_ReturnsSettingsWithinValidBounds()
    {
        var settings = GameWorkerSettings.CreateDefault();

        Assert.Equal(GameWorkerSettings.DefaultCommandQuantum, settings.CommandQuantumPerGameActivation);
        Assert.Equal(GameWorkerSettings.DefaultMaxQueueLength, settings.MaxQueueLengthPerGame);
        Assert.Equal(GameWorkerSettings.DefaultMaxActiveGames, settings.MaxActiveGames);
        Assert.Equal(GameWorkerSettings.DefaultMaxExternalAwaitDuration, settings.MaxExternalAwaitDuration);
        Assert.Equal(GameWorkerSettings.DefaultRateLimitBucketCapacity, settings.RateLimitBucketCapacity);
        Assert.Equal(GameWorkerSettings.DefaultRateLimitTokensPerSecond, settings.RateLimitTokensPerSecond);
    }

    [Theory]
    [InlineData(0, 256, 100, 5, 16, 64, "commandQuantumPerGameActivation")]
    [InlineData(8, 0, 100, 5, 16, 64, "maxQueueLengthPerGame")]
    [InlineData(8, 256, 0, 5, 16, 64, "maxActiveGames")]
    [InlineData(8, 256, 100, 0, 16, 64, "maxExternalAwaitDuration")]
    [InlineData(8, 256, 100, -1, 16, 64, "maxExternalAwaitDuration")]
    [InlineData(8, 256, 100, 5, 0, 64, "rateLimitBucketCapacity")]
    [InlineData(8, 256, 100, 5, 16, -1, "rateLimitTokensPerSecond")]
    public void Constructor_ThrowsArgumentOutOfRangeException_ForInvalidParameter(
        int commandQuantum,
        int maxQueueLength,
        int maxActiveGames,
        int awaitDurationSeconds,
        double bucketCapacity,
        double tokensPerSecond,
        string expectedParameterName)
    {
        var awaitDuration = TimeSpan.FromSeconds(awaitDurationSeconds);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GameWorkerSettings(
            commandQuantum,
            maxQueueLength,
            maxActiveGames,
            awaitDuration,
            bucketCapacity,
            tokensPerSecond));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void Constructor_AcceptsBoundaryValues_AtExactLowerBounds()
    {
        var settings = new GameWorkerSettings(1, 1, 1, TimeSpan.FromTicks(1), 1, 0);

        Assert.Equal(1, settings.CommandQuantumPerGameActivation);
        Assert.Equal(1, settings.MaxExternalAwaitDuration.Ticks);
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenAwaitDurationIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameWorkerSettings(
            8, 256, 100, TimeSpan.Zero, 16, 64));
    }
}
