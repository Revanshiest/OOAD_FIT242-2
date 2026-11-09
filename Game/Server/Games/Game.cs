namespace GameServer.Games;

using GameServer.Messaging;

public sealed class Game : IGameRecipient, IActor
{
    private readonly GameCommandQueue _commandQueue = new();
    private readonly Dictionary<string, IActor> _childActors = new();
    private bool _isAwaiting;
    private DateTimeOffset _awaitStartedAt;

    public Game(string gameId, GamePriority priority, IScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(scope);

        GameId = gameId;
        Priority = priority;
        Scope = scope;
    }

    public string GameId { get; }

    public string ActorId => GameId;

    public GamePriority Priority { get; }

    public IScope Scope { get; }

    public GameCommandQueue CommandQueue => _commandQueue;

    public bool IsAwaitingExternalEvent => _isAwaiting;

    public IReadOnlyCollection<IActor> ChildActors => _childActors.Values;

    public void Tell(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commandQueue.Enqueue(command);
    }

    public void SendMessage(IActor targetActor, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(targetActor);
        ArgumentNullException.ThrowIfNull(command);
        targetActor.Tell(command);
    }

    public IActor CreateChildActor(string childActorId, GamePriority priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childActorId);

        var childActor = new Game(childActorId, priority, new Scope());
        _childActors[childActorId] = childActor;
        return childActor;
    }

    public TimeSpan AwaitDuration(DateTimeOffset currentTime)
    {
        _ = _isAwaiting
            ? 0
            : throw new InvalidOperationException($"Game '{GameId}' is not awaiting an external event.");

        return currentTime - _awaitStartedAt;
    }

    public void SuspendForExternalEvent(DateTimeOffset currentTime)
    {
        _isAwaiting = true;
        _awaitStartedAt = currentTime;
    }

    public void ResumeFromExternalEvent()
    {
        _ = _isAwaiting
            ? 0
            : throw new InvalidOperationException($"Game '{GameId}' is not awaiting an external event.");

        _isAwaiting = false;
    }

    public void ExecuteNextCommand()
    {
        var batch = _commandQueue.DequeueBatch(1);
        var command = batch.Count > 0
            ? batch[0]
            : throw new InvalidOperationException($"Game '{GameId}' has no commands to execute.");

        command.Execute();
    }

    public (int DispatchedCount, int FailedCount) ExecuteBatch(
        int maxCommandCount,
        Action<ICommand, Exception>? onCommandFailed = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCommandCount, 1, nameof(maxCommandCount));
        int dispatched = 0;
        int failed = 0;

        while (dispatched + failed < maxCommandCount && _commandQueue.HasCommands && !_isAwaiting)
        {
            var command = _commandQueue.DequeueBatch(1)[0];
            try
            {
                command.Execute();
                dispatched++;
            }
            catch (Exception exception)
            {
                failed++;
                onCommandFailed?.Invoke(command, exception);
            }
        }

        return (dispatched, failed);
    }
}
