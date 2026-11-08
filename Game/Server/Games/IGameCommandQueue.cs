namespace GameServer.Games;

public interface IGameCommandQueue
{
    int Count { get; }

    bool HasCommands { get; }

    bool IsSealedByTickMarker { get; }

    void Enqueue(ICommand command);

    void EnqueueTickMarker(ICommand tickCommand);

    IReadOnlyList<ICommand> DequeueBatch(int maxCommandCount);

    IReadOnlyList<string> GetUnprocessedCommandsSnapshot();
}

