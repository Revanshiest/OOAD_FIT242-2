namespace GameServer.Resharding;

using GameServer.Messaging;

public sealed class BackupTransitThread : ITransitOverflowSink
{
    public const int DefaultTransitQueueCapacity = 8192;
    private readonly BoundedMessageQueue<TransitEnvelope> _transitQueue;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Thread _thread;

    public BackupTransitThread()
        : this(DefaultTransitQueueCapacity)
    {
    }

    public BackupTransitThread(int transitQueueCapacity)
    {
        _transitQueue = new BoundedMessageQueue<TransitEnvelope>(transitQueueCapacity);
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "backup-transit"
        };
    }

    public void Overflow(InboundMessage message, IMessageQueueWriter<InboundMessage> targetWriter)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetWriter);

        _transitQueue.WriteBlocking(new TransitEnvelope(message, targetWriter));
    }

    public void Start()
    {
        _thread.Start();
    }

    public void Stop()
    {
        _stopSource.Cancel();
    }

    public void Join()
    {
        _thread.Join();
    }

    private void RunLoop()
    {
        try
        {
            while (!_stopSource.IsCancellationRequested)
            {
                var transitEnvelope = _transitQueue.ReadBlocking(_stopSource.Token);

                transitEnvelope.TargetWriter.WriteBlocking(transitEnvelope.Message);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
