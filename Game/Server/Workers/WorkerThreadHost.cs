namespace GameServer.Workers;

using GameServer.Messaging;

public sealed class WorkerThreadHost : IWorkerThreadHost
{
    private static readonly TimeSpan WorkerIdlePollInterval = TimeSpan.FromMilliseconds(5);
    private readonly int _threadId;
    private readonly IGameWorker _worker;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Thread _thread;
    private int _started;

    public event Action<Exception>? UnhandledExceptionOccurred;

    public WorkerThreadHost(
        int threadId,
        IGameWorker worker,
        INonBlockingMessageWriter<InboundMessage> messageWriter,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threadId, nameof(threadId));
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(messageWriter);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _threadId = threadId;
        _worker = worker;
        MessageWriter = messageWriter;
        _timeProvider = timeProvider;
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = $"worker-thread-{threadId}"
        };
    }

    public int ThreadId => _threadId;

    public INonBlockingMessageWriter<InboundMessage> MessageWriter { get; }

    public bool IsShutdownCompleted => _worker.IsShutdownCompleted;

    public Exception? LastUnhandledException { get; private set; }

    public bool IsRunning => Volatile.Read(ref _started) == 1 && !_stopSource.IsCancellationRequested;

    public void Start()
    {
        var shouldStart = Interlocked.CompareExchange(ref _started, 1, 0) == 0;
        Action startAction = shouldStart ? _thread.Start : delegate { };
        startAction();
    }

    public void Stop()
    {
        var shouldStop = !_stopSource.IsCancellationRequested;
        Action stopAction = shouldStop ? _stopSource.Cancel : delegate { };
        stopAction();
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
                var cycleResult = RunProtectedCycle();
                var performedWork = cycleResult is not null && (cycleResult.DispatchedCommandCount > 0 || cycleResult.RoutedMessageCount > 0);

                var shouldWait = !performedWork || !_worker.HasReadyWork;
                _ = shouldWait && WaitForIncomingMessage();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool WaitForIncomingMessage()
    {
        _worker.WaitForIncomingMessage(WorkerIdlePollInterval, _stopSource.Token);
        return true;
    }

    private WorkerCycleResult? RunProtectedCycle()
    {
        try
        {
            return _worker.ExecuteCycle(_timeProvider.GetUtcNow());
        }
        catch (Exception exception)
        {
            LastUnhandledException = exception;
            UnhandledExceptionOccurred?.Invoke(exception);
            return null;
        }
    }
}
