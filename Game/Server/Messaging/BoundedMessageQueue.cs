using System.Diagnostics;

namespace GameServer.Messaging;

public sealed class BoundedMessageQueue<TMessage> : IMessageQueueWriter<TMessage>, IMessageQueueReader<TMessage>
{
    private const int WaitSliceMilliseconds = 50;
    private readonly int _capacity;
    private readonly Queue<TMessage> _queue;
    private readonly object _syncLock = new();
    private long _tailPointer;
    private long _headPointer;

    public BoundedMessageQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1, nameof(capacity));
        _capacity = capacity;
        _queue = new Queue<TMessage>(capacity);
    }

    public int PendingCount
    {
        get
        {
            lock (_syncLock)
            {
                return _queue.Count;
            }
        }
    }

    public long TailPointer
    {
        get
        {
            lock (_syncLock)
            {
                return _tailPointer;
            }
        }
    }

    public long HeadPointer
    {
        get
        {
            lock (_syncLock)
            {
                return _headPointer;
            }
        }
    }

    public bool TryWrite(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_syncLock)
        {
            var canWrite = _queue.Count < _capacity;
            _ = canWrite && EnqueueUnderLock(message);
            return canWrite;
        }
    }

    private bool EnqueueUnderLock(TMessage message)
    {
        _queue.Enqueue(message);
        _tailPointer++;
        Monitor.PulseAll(_syncLock);
        return true;
    }

    public void WriteBlocking(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_syncLock)
        {
            while (_queue.Count >= _capacity)
            {
                Monitor.Wait(_syncLock);
            }

            _queue.Enqueue(message);
            _tailPointer++;
            Monitor.PulseAll(_syncLock);
        }
    }

    public bool WaitForMessage(TimeSpan waitTimeout, CancellationToken cancellationToken)
    {
        lock (_syncLock)
        {
            var remaining = waitTimeout;
            var stopwatch = Stopwatch.StartNew();

            while (_queue.Count == 0 && remaining > TimeSpan.Zero && !cancellationToken.IsCancellationRequested)
            {
                var slice = (int)Math.Min(remaining.TotalMilliseconds, WaitSliceMilliseconds);
                Monitor.Wait(_syncLock, Math.Max(slice, 1));
                remaining = waitTimeout - stopwatch.Elapsed;
            }

            return _queue.Count > 0 && !cancellationToken.IsCancellationRequested;
        }
    }

    public TMessage ReadBlocking(CancellationToken cancellationToken)
    {
        lock (_syncLock)
        {
            while (_queue.Count == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Monitor.Wait(_syncLock, WaitSliceMilliseconds);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var message = _queue.Dequeue();
            _headPointer++;
            Monitor.PulseAll(_syncLock);
            return message;
        }
    }

    public IReadOnlyList<TMessage> DrainAllAvailable()
    {
        lock (_syncLock)
        {
            var drainedMessages = new List<TMessage>(_queue.Count);
            while (_queue.Count > 0)
            {
                drainedMessages.Add(_queue.Dequeue());
                _headPointer++;
            }

            Monitor.PulseAll(_syncLock);
            return drainedMessages;
        }
    }
}
