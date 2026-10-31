using GameServer.Messaging;
using GameServer.Resharding;

namespace Game.Tests.Server.Resharding;

public class GameMigrationCoordinatorTests
{
    [Fact]
    public void BeginThreadMigration_UpdatesReshardingTable_AndPostsMigrateOutToSourceWorker()
    {
        var reshardingTable = new ReshardingTable(2);
        var postedMessages = new List<(int ThreadId, InboundMessage Message)>();
        var messagePoster = new RecordingMessagePoster(postedMessages);
        var migrationCoordinator = new GameMigrationCoordinator(reshardingTable, messagePoster);

        migrationCoordinator.BeginThreadMigration(new GameMigrationRequest("game-1", 0, 1));

        Assert.Equal(1, reshardingTable.ResolveRoute("game-1"));
        var (postedThreadId, postedMessage) = Assert.Single(postedMessages);
        Assert.Equal(0, postedThreadId);
        Assert.Equal("game-1", postedMessage.GameId);
        Assert.Equal(ServerCommandNames.MigrateOut, postedMessage.CommandName);
        Assert.Contains("\"targetThreadId\":1", postedMessage.CommandArguments);
    }

    [Fact]
    public void BeginThreadMigration_ThrowsInvalidOperationException_WhenSourceWorkerRejectsPosting()
    {
        var migrationCoordinator = new GameMigrationCoordinator(
            new ReshardingTable(2),
            new RejectingMessagePoster());

        Assert.Throws<InvalidOperationException>(() =>
            migrationCoordinator.BeginThreadMigration(new GameMigrationRequest("game-1", 0, 1)));
    }

    private sealed class RecordingMessagePoster(List<(int ThreadId, InboundMessage Message)> postedMessages) : IWorkerMessagePoster
    {
        public bool TryPost(int threadId, InboundMessage message)
        {
            postedMessages.Add((threadId, message));
            return true;
        }
    }

    private sealed class RejectingMessagePoster : IWorkerMessagePoster
    {
        public bool TryPost(int threadId, InboundMessage message)
        {
            return false;
        }
    }
}
