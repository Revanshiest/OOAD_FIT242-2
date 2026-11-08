namespace GameServer.Workers;

using Game = GameServer.Games.Game;

public sealed record ScheduleOutcome(
    int DispatchedCommandCount,
    int FailedCommandCount,
    IReadOnlyList<string> TerminatedGameIds);

public interface IGameScheduler
{
    int TotalQueuedCommandCount { get; }

    bool HasReadyCommands { get; }

    event Action<ICommand, Exception>? CommandExecutionFailed;

    void Register(Game game);

    Game? FindGame(string gameId);

    int RegisteredGameCount { get; }

    IReadOnlyCollection<string> RegisteredGameIds { get; }

    void NotifyCommandsQueued(Game game);

    ScheduleOutcome ScheduleCycle(DateTimeOffset currentTime);

    void Unregister(string gameId, GameTerminationReason terminationReason);
}
