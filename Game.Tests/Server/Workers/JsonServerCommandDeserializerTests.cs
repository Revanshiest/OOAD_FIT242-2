namespace Game.Tests.Server.Workers;

using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Workers;
using Game = GameServer.Games.Game;

public class JsonServerCommandDeserializerTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly JsonServerCommandDeserializer _deserializer = new(new FixedTimeProvider(TestTimestamp));

    [Fact]
    public void Deserialize_ClampsSpeedComponents_ToMaximumMagnitude()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var message = new InboundMessage(
            0,
            "game-1",
            "token",
            ServerCommandNames.CreateItem,
            """{"itemId":"ship1","type":"Ship","position":[1,2],"speed":[999999,5]}""");

        _deserializer.Deserialize(message, targetGame).Execute();

        Assert.Equal(
            new Vector(new[] { JsonServerCommandDeserializer.MaxSpeedMagnitude, 5 }),
            targetGame.Scope.GetItem("ship1").GetProperty(GameItemProperty.Speed));
    }

    [Fact]
    public void Deserialize_ClampsPositionComponents_ToMaximumMagnitude()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var message = new InboundMessage(
            0,
            "game-1",
            "token",
            ServerCommandNames.CreateItem,
            """{"itemId":"ship1","type":"Ship","position":[-999999,2],"speed":[0,1]}""");

        _deserializer.Deserialize(message, targetGame).Execute();

        Assert.Equal(
            new Vector(new[] { -JsonServerCommandDeserializer.MaxCoordinateMagnitude, 2 }),
            targetGame.Scope.GetItem("ship1").GetProperty(GameItemProperty.Position));
    }

    [Fact]
    public void Deserialize_KeepsComponentsAtExactMagnitudeBoundary()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var boundarySpeed = JsonServerCommandDeserializer.MaxSpeedMagnitude;
        var message = new InboundMessage(
            0,
            "game-1",
            "token",
            ServerCommandNames.CreateItem,
            $$"""{"itemId":"ship1","type":"Ship","position":[0,0],"speed":[{{boundarySpeed}},-{{boundarySpeed}}]}""");

        _deserializer.Deserialize(message, targetGame).Execute();

        Assert.Equal(
            new Vector(new[] { boundarySpeed, -boundarySpeed }),
            targetGame.Scope.GetItem("ship1").GetProperty(GameItemProperty.Speed));
    }

    [Fact]
    public void Deserialize_ThrowsKeyNotFoundException_WhenRequiredArgumentIsMissing()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var message = new InboundMessage(0, "game-1", "token", ServerCommandNames.Move, """{"wrongField":"x"}""");

        Assert.Throws<KeyNotFoundException>(() => _deserializer.Deserialize(message, targetGame));
    }

    [Fact]
    public void Deserialize_ThrowsArgumentException_WhenCommandNameIsUnknown()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var message = new InboundMessage(0, "game-1", "token", "Teleport", "{}");

        Assert.Throws<ArgumentException>(() => _deserializer.Deserialize(message, targetGame));
    }

    [Fact]
    public void Deserialize_ProducesSuspendCommand_BoundToTargetGame()
    {
        var targetGame = new Game("game-1", GamePriority.Normal, new Scope());
        var message = new InboundMessage(0, "game-1", "token", ServerCommandNames.Suspend, "{}");

        _deserializer.Deserialize(message, targetGame).Execute();

        Assert.True(targetGame.IsAwaitingExternalEvent);
    }
}
