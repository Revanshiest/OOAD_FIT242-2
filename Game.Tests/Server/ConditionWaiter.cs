namespace Game.Tests.Server;

public static class ConditionWaiter
{
    private const int DefaultTimeoutMilliseconds = 5000;

    public static bool WaitUntil(Func<bool> condition)
    {
        return SpinWait.SpinUntil(condition, DefaultTimeoutMilliseconds);
    }
}
