using System.Text.Json;

namespace GameServer.Resharding;

using GameServer.Messaging;

public interface IWorkerMessagePoster
{
    bool TryPost(int threadId, InboundMessage message);
}

public sealed record GameMigrationRequest(string GameId, int SourceThreadId, int TargetThreadId);

public sealed class GameMigrationCoordinator
{
    private readonly ReshardingTable _reshardingTable;
    private readonly IWorkerMessagePoster _messagePoster;

    public GameMigrationCoordinator(ReshardingTable reshardingTable, IWorkerMessagePoster messagePoster)
    {
        _reshardingTable = reshardingTable ?? throw new ArgumentNullException(nameof(reshardingTable));
        _messagePoster = messagePoster ?? throw new ArgumentNullException(nameof(messagePoster));
    }

    public void BeginThreadMigration(GameMigrationRequest migrationRequest)
    {
        ArgumentNullException.ThrowIfNull(migrationRequest);

        _reshardingTable.SetRoute(migrationRequest.GameId, migrationRequest.TargetThreadId);

        var migrateOutArguments = JsonSerializer.Serialize(
            new { targetThreadId = migrationRequest.TargetThreadId },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var migrateOutMessage = new InboundMessage(
            migrationRequest.SourceThreadId,
            migrationRequest.GameId,
            "system",
            ServerCommandNames.MigrateOut,
            migrateOutArguments);

        if (!_messagePoster.TryPost(migrationRequest.SourceThreadId, migrateOutMessage))
        {
            throw new InvalidOperationException($"Source worker '{migrationRequest.SourceThreadId}' rejected the migration request.");
        }
    }
}
