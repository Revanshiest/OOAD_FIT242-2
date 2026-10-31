namespace GameServer.Workers;

using GameServer.Games;
using Game = GameServer.Games.Game;

public interface IGameFactory
{
    Game Create(string gameId);
}

public sealed class DefaultGameFactory : IGameFactory
{
    public Game Create(string gameId)
    {
        return new Game(gameId, GamePriority.Normal, new Scope());
    }
}
