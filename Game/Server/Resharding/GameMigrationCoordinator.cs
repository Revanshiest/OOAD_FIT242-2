namespace GameServer.Resharding;

using System.Text.Json;
using GameServer.Messaging;

public interface IWorkerMessagePoster
{
    bool TryPost(int threadId, InboundMessage message);
}

public sealed record GameMigrationRequest(string GameId, int SourceThreadId, int TargetThreadId);

public sealed class GameMigrationCoordinator : IGameMigrationCoordinator
{
    private readonly IWorkerMessagePoster _messagePoster;
    private readonly IReshardingTable _reshardingTable;

    public GameMigrationCoordinator(
        IWorkerMessagePoster messagePoster,
        IReshardingTable reshardingTable)
    {
        ArgumentNullException.ThrowIfNull(messagePoster);
        ArgumentNullException.ThrowIfNull(reshardingTable);

        _messagePoster = messagePoster;
        _reshardingTable = reshardingTable;
    }

    public GameMigrationCoordinator(
        IReshardingTable reshardingTable,
        IWorkerMessagePoster messagePoster)
        : this(messagePoster, reshardingTable)
    {
    }

    public void BeginThreadMigration(GameMigrationRequest migrationRequest)
    {
        ArgumentNullException.ThrowIfNull(migrationRequest);
        var migrateOutArguments = JsonSerializer.Serialize(new
        {
            gameId = migrationRequest.GameId,
            targetThreadId = migrationRequest.TargetThreadId
        });

        var migrateOutMessage = new InboundMessage(
            migrationRequest.SourceThreadId,
            migrationRequest.GameId,
            "system",
            ServerCommandNames.MigrateOut,
            migrateOutArguments);

        _ = _messagePoster.TryPost(migrationRequest.SourceThreadId, migrateOutMessage)
            ? 0
            : throw new InvalidOperationException($"Source worker '{migrationRequest.SourceThreadId}' rejected the migration request.");

        _reshardingTable.SetRoute(migrationRequest.GameId, migrationRequest.TargetThreadId);
    }

    public void CompleteThreadMigration(string gameId, int targetThreadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        _reshardingTable.SetRoute(gameId, targetThreadId);
    }
}
