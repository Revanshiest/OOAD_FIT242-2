using System.Collections.Concurrent;

namespace GameServer.Resharding;

using GameServer.Messaging;

public sealed class BackupTransitThread : ITransitOverflowSink
{
    public const int DefaultTransitQueueCapacity = 8192;
    private const int MaximumDeliveryAttempts = 5;
    private const int DeliveryRetryDelayMilliseconds = 5;
    private readonly ConcurrentQueue<TransitEnvelope> _transitQueue = new();
    private readonly AutoResetEvent _hasWork = new(false);
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Thread _thread;
    private int _started;

    public BackupTransitThread()
        : this(DefaultTransitQueueCapacity)
    {
    }

    public BackupTransitThread(int transitQueueCapacity)
    {
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "backup-transit"
        };
    }

    public bool IsRunning => Volatile.Read(ref _started) == 1 && !_stopSource.IsCancellationRequested;

    public void Overflow(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetWriter);

        Start();
        _transitQueue.Enqueue(new TransitEnvelope(message, targetWriter));
        _hasWork.Set();
    }

    public void Start()
    {
        var shouldStart = Interlocked.CompareExchange(ref _started, 1, 0) == 0;
        Action startAction = shouldStart ? _thread.Start : delegate { };
        startAction();
    }

    public void Stop()
    {
        var shouldStop = !_stopSource.IsCancellationRequested;
        Action stopAction = shouldStop ? StopAndSignal : delegate { };
        stopAction();
    }

    private void StopAndSignal()
    {
        _stopSource.Cancel();
        _hasWork.Set();
    }

    public void Join()
    {
        while (Volatile.Read(ref _started) == 1 && _thread.IsAlive)
        {
            _thread.Join();
            break;
        }
    }

    private void RunLoop()
    {
        var waitHandles = new WaitHandle[] { _hasWork, _stopSource.Token.WaitHandle };

        try
        {
            while (!_stopSource.IsCancellationRequested)
            {
                var workProcessed = ProcessPendingQueue();
                _ = !workProcessed && WaitHandle.WaitAny(waitHandles) >= 0;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool ProcessPendingQueue()
    {
        var hasItem = _transitQueue.TryDequeue(out var transitEnvelope);
        _ = hasItem && DeliverTransitEnvelope(transitEnvelope!);
        return hasItem;
    }

    private bool DeliverTransitEnvelope(TransitEnvelope transitEnvelope)
    {
        var delivered = transitEnvelope.TargetWriter.TryWrite(transitEnvelope.Message);

        for (var retry = 0; retry < MaximumDeliveryAttempts && !delivered && !_stopSource.IsCancellationRequested; retry++)
        {
            _stopSource.Token.WaitHandle.WaitOne(DeliveryRetryDelayMilliseconds);
            delivered = transitEnvelope.TargetWriter.TryWrite(transitEnvelope.Message);
        }

        while (!delivered && !_stopSource.IsCancellationRequested)
        {
            transitEnvelope.TargetWriter.WriteBlocking(transitEnvelope.Message);
            break;
        }

        return true;
    }
}
