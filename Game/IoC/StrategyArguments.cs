namespace Game.IoC;

public static class StrategyArguments
{
    public static T Get<T>(object[] arguments, int index)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.Length > index
            ? (T)arguments[index]
            : throw new ArgumentException($"Strategy argument at index {index} is required, provided {arguments.Length}.", nameof(arguments));
    }
}
