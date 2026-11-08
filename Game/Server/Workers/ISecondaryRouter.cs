namespace GameServer.Workers;

using GameServer.Messaging;

public interface ISecondaryRouter
{
    IReadOnlyCollection<IGameRecipient> Recipients { get; }

    IGameRecipient? FindRecipient(string gameId);

    void RegisterRecipient(string gameId, IGameRecipient recipient);

    void RemoveRecipient(string gameId);

    bool RouteMessage(
        InboundMessage message,
        bool isWorkerSealedByTick,
        IReadOnlyDictionary<string, int> migratingTargetByGameId);
}

