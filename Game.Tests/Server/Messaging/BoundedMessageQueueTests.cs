using System.Collections.Concurrent;
using GameServer.Messaging;

namespace Game.Tests.Server.Messaging;

public class BoundedMessageQueueTests
{
    private const int QueueCapacity = 4;
    private const int TaskCompletionTimeoutMilliseconds = 5000;

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenCapacityIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedMessageQueue<string>(0));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedMessageQueue<string>(-1));
    }

    [Fact]
    public void TryWrite_IntoEmptyQueue_AcceptsMessageAndReportsPendingCount()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);

        var writeAccepted = queue.TryWrite("m1");

        Assert.True(writeAccepted);
        Assert.Equal(1, queue.PendingCount);
    }

    [Theory]
    [InlineData(QueueCapacity, true)]
    [InlineData(QueueCapacity + 1, false)]
    public void TryWrite_AcceptsMessagesOnlyBelowFullCapacityBoundary(int writtenMessageCount, bool expectedLastWriteAccepted)
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);

        var writeResults = Enumerable
            .Range(0, writtenMessageCount)
            .Select(_ => queue.TryWrite("message"))
            .ToList();

        Assert.Equal(expectedLastWriteAccepted, writeResults.Last());
        Assert.All(writeResults.Take(writtenMessageCount - 1), result => Assert.True(result));
        Assert.Equal(Math.Min(writtenMessageCount, QueueCapacity), queue.PendingCount);
    }

    [Fact]
    public void DrainAllAvailable_ReturnsEmptyCollection_WhenQueueIsEmpty()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);

        var drainedMessages = queue.DrainAllAvailable();

        Assert.Empty(drainedMessages);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void DrainAllAvailable_ReturnsMessagesInFifoOrder()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        var writtenMessages = new[] { "first", "second", "third", "fourth" };
        Array.ForEach(writtenMessages, message => queue.TryWrite(message));

        var drainedMessages = queue.DrainAllAvailable();

        Assert.Equal(writtenMessages, drainedMessages);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void DrainAllAvailable_ReturnsOnlyMessagesWrittenBeforeTheCall()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        queue.TryWrite("before");

        var drainedMessages = queue.DrainAllAvailable();
        queue.TryWrite("after");

        Assert.Equal(new[] { "before" }, drainedMessages);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void ReadBlocking_ReturnsPrecedingMessage_WhenQueueAlreadyFilled()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        queue.TryWrite("stored");

        var readMessage = queue.ReadBlocking(CancellationToken.None);

        Assert.Equal("stored", readMessage);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task ReadBlocking_WaitsForProducerMessage_WhenQueueIsEmptyAtCallTime()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        var readTask = Task.Run(() => queue.ReadBlocking(CancellationToken.None));

        await Task.Yield();
        queue.TryWrite("delayed");

        var readMessage = await readTask.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds));

        Assert.Equal("delayed", readMessage);
    }

    [Fact]
    public async Task ReadBlocking_ThrowsOperationCanceledException_WhenCancellationTokenIsCancelled()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        using var cancellationSource = new CancellationTokenSource();
        var readTask = Task.Run(() => queue.ReadBlocking(cancellationSource.Token));

        await Task.Yield();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            readTask.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds)));
    }

    [Fact]
    public async Task WriteBlocking_SuspendsProducer_WhileQueueStaysAtFullCapacity()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        Enumerable.Range(0, QueueCapacity).ToList().ForEach(_ => queue.TryWrite("filler"));
        var blockingWriteTask = Task.Run(() => queue.WriteBlocking("overflow"));

        var writerReachedBlockingWait = ConditionWaiter.WaitUntil(() => blockingWriteTask.Status == TaskStatus.Running);

        Assert.True(writerReachedBlockingWait);
        Assert.False(blockingWriteTask.IsCompleted);
        Assert.Equal(QueueCapacity, queue.PendingCount);

        queue.DrainAllAvailable();
        await blockingWriteTask.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds));
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public async Task WriteBlocking_CompletesAfterConsumerFreesSlot()
    {
        var queue = new BoundedMessageQueue<string>(QueueCapacity);
        Enumerable.Range(0, QueueCapacity).ToList().ForEach(_ => queue.TryWrite("filler"));
        var blockingWriteTask = Task.Run(() => queue.WriteBlocking("overflow"));
        ConditionWaiter.WaitUntil(() => blockingWriteTask.Status == TaskStatus.Running);

        queue.ReadBlocking(CancellationToken.None);

        await blockingWriteTask.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds));

        Assert.Equal(QueueCapacity, queue.PendingCount);
    }

    [Fact]
    public async Task ConcurrentProducers_DeliverEveryMessageExactlyOnce_ToSingleConsumer()
    {
        const int producerCount = 4;
        const int messagesPerProducer = 500;
        const int totalMessageCount = producerCount * messagesPerProducer;
        var queue = new BoundedMessageQueue<int>(QueueCapacity);
        var receivedMessages = new ConcurrentBag<int>();

        var producers = Enumerable
            .Range(0, producerCount)
            .Select(producerIndex => Task.Run(() =>
                PublishProducerMessages(queue, producerIndex, messagesPerProducer, static _ => { })))
            .ToArray();
        var consumer = Task.Run(() => ConsumeUntilTotal(queue, receivedMessages, totalMessageCount));

        await Task.WhenAll(producers);
        await consumer.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds));

        Assert.Equal(totalMessageCount, receivedMessages.Count);
        Assert.Equal(0, queue.PendingCount);
        Assert.All(
            Enumerable.Range(0, producerCount),
            producerIndex => Assert.Equal(
                messagesPerProducer,
                receivedMessages.Count(message =>
                    message >= producerIndex * messagesPerProducer
                    && message < (producerIndex + 1) * messagesPerProducer)));
        Assert.Equal(totalMessageCount, receivedMessages.Distinct().Count());
    }

    [Fact]
    public async Task PendingCount_ReturnsToZero_AfterConcurrentProducersAndConsumerComplete()
    {
        const int producerCount = 4;
        const int messagesPerProducer = 500;
        const int totalMessageCount = producerCount * messagesPerProducer;
        var queue = new BoundedMessageQueue<int>(QueueCapacity);
        var receivedMessages = new ConcurrentBag<int>();

        var producers = Enumerable
            .Range(0, producerCount)
            .Select(producerIndex => Task.Run(() =>
                PublishProducerMessages(queue, producerIndex, messagesPerProducer, static _ => { })))
            .ToArray();
        var consumer = Task.Run(() => ConsumeUntilTotal(queue, receivedMessages, totalMessageCount));

        await Task.WhenAll(producers);
        await consumer.WaitAsync(TimeSpan.FromMilliseconds(TaskCompletionTimeoutMilliseconds));

        Assert.Equal(totalMessageCount, receivedMessages.Count);
        Assert.Equal(0, queue.PendingCount);
    }

    private static void PublishProducerMessages(
        BoundedMessageQueue<int> queue,
        int producerIndex,
        int messagesPerProducer,
        Action<int> reportObservedPendingCount)
    {
        var producerOffset = producerIndex * messagesPerProducer;

        Enumerable
            .Range(0, messagesPerProducer)
            .ToList()
            .ForEach(sequenceNumber =>
            {
                PublishMessageWithRetries(queue, producerOffset + sequenceNumber);
                reportObservedPendingCount(queue.PendingCount);
            });
    }

    private static void PublishMessageWithRetries(BoundedMessageQueue<int> queue, int messageValue)
    {
        var messageAccepted = SpinWait.SpinUntil(
            () => queue.TryWrite(messageValue),
            TaskCompletionTimeoutMilliseconds);

        if (!messageAccepted)
        {
            throw new TimeoutException($"Producer failed to write message {messageValue} within the timeout.");
        }
    }

    private static void ConsumeUntilTotal(BoundedMessageQueue<int> queue, ConcurrentBag<int> receivedMessages, int totalMessageCount)
    {
        while (receivedMessages.Count < totalMessageCount)
        {
            queue.DrainAllAvailable().ToList().ForEach(receivedMessages.Add);
        }
    }
}
