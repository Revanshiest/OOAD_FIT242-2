using GameServer.Messaging;

namespace GameServer.Games;

public sealed class Game : IGameRecipient
{
    private readonly GameCommandQueue _commandQueue = new();
    private bool _isAwaitingExternalEvent;
    private DateTimeOffset _awaitStartedAt;

    public Game(string gameId, GamePriority priority, IScope scope)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("Game identifier is required.", nameof(gameId));
        }

        GameId = gameId;
        Priority = priority;
        Scope = scope;
    }

    public string GameId { get; }

    public GamePriority Priority { get; }

    public IScope Scope { get; }

    public GameCommandQueue CommandQueue => _commandQueue;

    public bool IsAwaitingExternalEvent => _isAwaitingExternalEvent;

    public TimeSpan AwaitDuration(DateTimeOffset currentTime)
    {
        if (!_isAwaitingExternalEvent)
        {
            throw new InvalidOperationException($"Game '{GameId}' is not awaiting an external event.");
        }

        return currentTime - _awaitStartedAt;
    }

    public void SuspendForExternalEvent(DateTimeOffset currentTime)
    {
        _isAwaitingExternalEvent = true;
        _awaitStartedAt = currentTime;
    }

    public void ResumeFromExternalEvent()
    {
        if (!_isAwaitingExternalEvent)
        {
            throw new InvalidOperationException($"Game '{GameId}' is not awaiting an external event.");
        }

        _isAwaitingExternalEvent = false;
    }

    public void ExecuteNextCommand()
    {
        _commandQueue.DequeueBatch(1).Single().Execute();
    }
}
