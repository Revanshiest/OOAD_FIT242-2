using GameServer.Messaging;
using GameServer.Resharding;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Resharding;

public class BackupTransitThreadTests
{
    [Fact]
    public void StartedBackupThread_PushesTransitEnvelopesIntoTargetWriters()
    {
        var firstTargetWriter = new RecordingMessageWriter();
        var secondTargetWriter = new RecordingMessageWriter();
        var backupTransitThread = new BackupTransitThread();

        backupTransitThread.Start();
        backupTransitThread.Overflow(new InboundMessage(0, "game-1", "token", "Move", "{}"), firstTargetWriter);
        backupTransitThread.Overflow(new InboundMessage(1, "game-2", "token", "Move", "{}"), secondTargetWriter);

        var allMessagesDelivered = ConditionWaiter.WaitUntil(() =>
            firstTargetWriter.WrittenMessages.Count == 1 && secondTargetWriter.WrittenMessages.Count == 1);

        backupTransitThread.Stop();
        backupTransitThread.Join();

        Assert.True(allMessagesDelivered);
        Assert.Equal("game-1", firstTargetWriter.WrittenMessages[0].GameId);
        Assert.Equal("game-2", secondTargetWriter.WrittenMessages[0].GameId);
    }

    [Fact]
    public void Stop_TerminatesIdleBackupThread()
    {
        var backupTransitThread = new BackupTransitThread();
        backupTransitThread.Start();

        backupTransitThread.Stop();
        backupTransitThread.Join();

        Assert.True(true);
    }
}
