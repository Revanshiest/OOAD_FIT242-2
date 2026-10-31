namespace GameServer.Games;

public sealed class GameCommandQueue
{
    private readonly Queue<ICommand> _commands = new();
    private bool _isSealedByTickMarker;

    public int Count => _commands.Count;

    public bool HasCommands => _commands.Count > 0;

    public bool IsSealedByTickMarker => _isSealedByTickMarker;

    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_isSealedByTickMarker)
        {
            throw new InvalidOperationException("Command queue is sealed by the tick marker and rejects client commands.");
        }

        _commands.Enqueue(command);
    }

    public void EnqueueTickMarker(ICommand tickCommand)
    {
        if (_isSealedByTickMarker)
        {
            return;
        }

        Enqueue(tickCommand);
        _isSealedByTickMarker = true;
    }

    public IReadOnlyList<ICommand> DequeueBatch(int maxCommandCount)
    {
        if (maxCommandCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCommandCount), maxCommandCount, "Batch size must be positive.");
        }

        var drainedCommands = _commands.Take(maxCommandCount).ToArray();

        Enumerable
            .Range(0, drainedCommands.Length)
            .ToList()
            .ForEach(_ => _commands.Dequeue());

        return drainedCommands;
    }
}
