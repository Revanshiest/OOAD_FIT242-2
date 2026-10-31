namespace GameServer.Workers;

using Game = GameServer.Games.Game;

public sealed class SuspendGameCommand(Game targetGame, TimeProvider timeProvider) : ICommand
{
    public void Execute()
    {
        targetGame.SuspendForExternalEvent(timeProvider.GetUtcNow());
    }
}
