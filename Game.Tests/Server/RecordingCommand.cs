namespace Game.Tests.Server;

public sealed class RecordingCommand : ICommand
{
    public bool WasExecuted { get; private set; }

    public int ExecutionCount { get; private set; }

    public void Execute()
    {
        WasExecuted = true;
        ExecutionCount++;
    }
}
