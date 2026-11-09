namespace GameServer.Workers;

using Game = GameServer.Games.Game;

public sealed class SuspendGameCommand(Game targetGame, TimeProvider timeProvider) : ICommand
{
    private bool _resumeRequested;

    internal bool ResumeRequested => _resumeRequested;

    internal bool TryRequestResume()
    {
        if (_resumeRequested)
        {
            return false;
        }

        _resumeRequested = true;
        return true;
    }

    public void Execute()
    {
        if (!_resumeRequested)
        {
            targetGame.SuspendForExternalEvent(timeProvider.GetUtcNow());
        }
    }
}
