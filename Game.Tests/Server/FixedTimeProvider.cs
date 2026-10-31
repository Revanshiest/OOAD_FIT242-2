namespace Game.Tests.Server;

public sealed class FixedTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
    private DateTimeOffset _currentUtcNow = initialUtcNow;

    public override DateTimeOffset GetUtcNow() => _currentUtcNow;

    public void Advance(TimeSpan duration)
    {
        _currentUtcNow += duration;
    }
}
