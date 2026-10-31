using GameServer.Games;
using GameServer.Resharding;

namespace Game.Tests.Server.Resharding;

public class JsonServerSnapshotCodecTests
{
    private readonly JsonServerSnapshotCodec _snapshotCodec = new();

    [Fact]
    public void Serialize_And_Deserialize_RestoreSnapshotWithAllItems()
    {
        var sourceSnapshot = new GameSnapshot(
            "game-1",
            GamePriority.High,
            [
                new GameObjectSnapshot("ship1", """{"Type":"Ship","Position":[1,2],"Speed":[0,1]}"""),
                new GameObjectSnapshot("torpedo1", """{"Type":"Torpedo","Damage":5}""")
            ]);

        var restoredSnapshot = _snapshotCodec.Deserialize(_snapshotCodec.Serialize(sourceSnapshot));

        Assert.Equal("game-1", restoredSnapshot.GameId);
        Assert.Equal(GamePriority.High, restoredSnapshot.Priority);
        Assert.Equal(2, restoredSnapshot.Items.Count);
        Assert.Equal("ship1", restoredSnapshot.Items[0].ItemId);
        Assert.Contains("Position", restoredSnapshot.Items[0].SerializedProperties);
        Assert.Equal("torpedo1", restoredSnapshot.Items[1].ItemId);
        Assert.Contains("Damage", restoredSnapshot.Items[1].SerializedProperties);
    }

    [Fact]
    public void Serialize_ProducesJsonObjectPayload()
    {
        var sourceSnapshot = new GameSnapshot(
            "game-1",
            GamePriority.Normal,
            [new GameObjectSnapshot("ship1", """{"Type":"Ship"}""")]);

        var serializedPayload = _snapshotCodec.Serialize(sourceSnapshot);

        Assert.StartsWith("{", serializedPayload);
        Assert.Contains("\"gameId\":\"game-1\"", serializedPayload);
        Assert.Contains("\"priority\":\"Normal\"", serializedPayload);
    }

    [Fact]
    public void Deserialize_ThrowsArgumentException_WhenPriorityIsUnknown()
    {
        const string malformedPayload = """{"gameId":"game-1","priority":"Ultra","items":[]}""";

        Assert.Throws<ArgumentException>(() => _snapshotCodec.Deserialize(malformedPayload));
    }

    [Fact]
    public void Deserialize_ThrowsKeyNotFoundException_WhenRequiredFieldIsMissing()
    {
        const string malformedPayload = """{"priority":"Normal","items":[]}""";

        Assert.Throws<KeyNotFoundException>(() => _snapshotCodec.Deserialize(malformedPayload));
    }

    [Fact]
    public void Serialize_ThrowsArgumentNullException_WhenSnapshotIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _snapshotCodec.Serialize(null!));
    }
}
