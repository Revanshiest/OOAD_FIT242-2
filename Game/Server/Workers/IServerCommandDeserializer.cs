namespace GameServer.Workers;

using GameServer.Messaging;
using Game = GameServer.Games.Game;

public interface IServerCommandDeserializer
{
    ICommand Deserialize(InboundMessage message, Game targetGame);
}
