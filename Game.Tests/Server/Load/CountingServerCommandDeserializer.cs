namespace Game.Tests.Server.Load;

using GameServer.Messaging;
using GameServer.Workers;
using Game = GameServer.Games.Game;

public sealed class CountingServerCommandDeserializer(IServerCommandDeserializer innerDeserializer) : IServerCommandDeserializer
{
    private int _deserializedCount;

    public int DeserializedCount => Volatile.Read(ref _deserializedCount);

    public ICommand Deserialize(InboundMessage message, Game targetGame)
    {
        var command = innerDeserializer.Deserialize(message, targetGame);
        Interlocked.Increment(ref _deserializedCount);
        return command;
    }
}

public sealed record LoadRunResult(
    int SentMessages,
    int ProcessedMessages,
    double ElapsedTotalSeconds,
    double ProcessedMessagesPerSecond);
