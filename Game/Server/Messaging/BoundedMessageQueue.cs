using System.Threading.Channels;

namespace GameServer.Messaging;

public sealed class BoundedMessageQueue<TMessage> : IMessageQueueWriter<TMessage>, IMessageQueueReader<TMessage>
{
    private readonly Channel<TMessage> _channel;
    private int _pendingCount;

    public BoundedMessageQueue(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Queue capacity must be positive.");
        }

        _channel = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public bool TryWrite(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Interlocked.Increment(ref _pendingCount);

        var messageAccepted = _channel.Writer.TryWrite(message);

        if (!messageAccepted)
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        return messageAccepted;
    }

    public void WriteBlocking(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Interlocked.Increment(ref _pendingCount);

        if (_channel.Writer.TryWrite(message))
        {
            return;
        }

        _channel.Writer.WriteAsync(message).AsTask().GetAwaiter().GetResult();
    }

    public TMessage ReadBlocking()
    {
        var messageAvailable = _channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult();

        if (!messageAvailable || !_channel.Reader.TryRead(out var message))
        {
            throw new InvalidOperationException("Message queue was completed before the blocking read finished.");
        }

        Interlocked.Decrement(ref _pendingCount);
        return message;
    }

    public IReadOnlyList<TMessage> DrainAllAvailable()
    {
        var drainedMessages = new List<TMessage>();

        while (_channel.Reader.TryRead(out var message))
        {
            drainedMessages.Add(message);
            Interlocked.Decrement(ref _pendingCount);
        }

        return drainedMessages;
    }
}
