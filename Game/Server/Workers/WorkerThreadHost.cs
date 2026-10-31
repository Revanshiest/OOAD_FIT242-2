namespace GameServer.Workers;

using GameServer.Messaging;

public sealed class WorkerThreadHost
{
    public static readonly TimeSpan WorkerIdlePollInterval = TimeSpan.FromMilliseconds(50);
    private readonly GameWorker _worker;
    private readonly TimeProvider _timeProvider;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stopSource = new();

    public WorkerThreadHost(
        int threadId,
        GameWorker worker,
        IMessageQueueWriter<InboundMessage> messageWriter,
        TimeProvider timeProvider)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        MessageWriter = messageWriter ?? throw new ArgumentNullException(nameof(messageWriter));
        ThreadId = threadId;

        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = $"game-worker-{threadId}"
        };
    }

    public int ThreadId { get; }

    public IMessageQueueWriter<InboundMessage> MessageWriter { get; }

    public bool IsShutdownCompleted => _worker.IsShutdownCompleted;

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
                RunProtectedCycle();
                _worker.WaitForIncomingMessage(WorkerIdlePollInterval, _stopSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RunProtectedCycle()
    {
        try
        {
            _worker.ExecuteCycle(_timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}
