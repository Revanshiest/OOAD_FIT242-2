using GameServer.Games;
using Game.Tests.Server;

namespace Game.Tests.Server.Games;

public class GameCommandQueueTests
{
    [Fact]
    public void Enqueue_And_DequeueBatch_ReturnCommandsInFifoOrder()
    {
        var queue = new GameCommandQueue();
        var firstCommand = new RecordingCommand();
        var secondCommand = new RecordingCommand();

        queue.Enqueue(firstCommand);
        queue.Enqueue(secondCommand);

        Assert.Equal(2, queue.Count);
        Assert.Equal(new[] { firstCommand, secondCommand }, queue.DequeueBatch(10));
        Assert.False(queue.HasCommands);
    }

    [Fact]
    public void DequeueBatch_ReturnsAtMostRequestedCommandCount()
    {
        var queue = new GameCommandQueue();
        queue.Enqueue(new RecordingCommand());
        queue.Enqueue(new RecordingCommand());
        queue.Enqueue(new RecordingCommand());

        var drainedCommands = queue.DequeueBatch(2);

        Assert.Equal(2, drainedCommands.Count);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void DequeueBatch_ReturnsEmptyCollection_WhenQueueHasNoCommands()
    {
        var queue = new GameCommandQueue();

        var drainedCommands = queue.DequeueBatch(3);

        Assert.Empty(drainedCommands);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DequeueBatch_ThrowsArgumentOutOfRangeException_WhenBatchSizeIsNotPositive(int maxCommandCount)
    {
        var queue = new GameCommandQueue();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.DequeueBatch(maxCommandCount));
    }

    [Fact]
    public void Enqueue_ThrowsInvalidOperationException_AfterTickMarkerSealsQueue()
    {
        var queue = new GameCommandQueue();
        queue.EnqueueTickMarker(new RecordingCommand());

        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(new RecordingCommand()));
    }

    [Fact]
    public void EnqueueTickMarker_PlacesMarkerCommandLast_AndSealsQueue()
    {
        var queue = new GameCommandQueue();
        var clientCommand = new RecordingCommand();
        var tickMarkerCommand = new RecordingCommand();
        queue.Enqueue(clientCommand);

        queue.EnqueueTickMarker(tickMarkerCommand);

        Assert.True(queue.IsSealedByTickMarker);
        Assert.Equal(new ICommand[] { clientCommand, tickMarkerCommand }, queue.DequeueBatch(10));
    }

    [Fact]
    public void DequeueBatch_DequeuesDirectly_WithoutIntermediateListAllocation()
    {
        var queue = new GameCommandQueue();
        var cmd1 = new RecordingCommand();
        var cmd2 = new RecordingCommand();
        var cmd3 = new RecordingCommand();

        queue.Enqueue(cmd1);
        queue.Enqueue(cmd2);
        queue.Enqueue(cmd3);

        var batch = queue.DequeueBatch(2);

        Assert.Equal(2, batch.Count);
        Assert.Same(cmd1, batch[0]);
        Assert.Same(cmd2, batch[1]);
        Assert.Equal(1, queue.Count);

        var remaining = queue.DequeueBatch(5);
        Assert.Single(remaining);
        Assert.Same(cmd3, remaining[0]);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var queue = new GameCommandQueue();

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }
}
