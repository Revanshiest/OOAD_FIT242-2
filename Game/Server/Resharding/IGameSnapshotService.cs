namespace GameServer.Resharding;

using Game = GameServer.Games.Game;

public interface IGameSnapshotService
{
    GameSnapshot CaptureSnapshot(Game game);

    Game RestoreGame(GameSnapshot snapshot);
}

