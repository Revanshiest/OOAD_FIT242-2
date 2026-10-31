using System.Text;
using System.Text.Json;

namespace GameServer.Resharding;

using GameServer.Games;

public sealed class JsonServerSnapshotCodec : ISnapshotCodec
{
    private const string GameIdFieldName = "gameId";
    private const string PriorityFieldName = "priority";
    private const string ItemsFieldName = "items";
    private const string ItemIdFieldName = "itemId";
    private const string PropertiesFieldName = "properties";

    public string Serialize(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var propertiesStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(propertiesStream))
        {
            WriteSnapshot(writer, snapshot);
        }

        return Encoding.UTF8.GetString(propertiesStream.ToArray());
    }

    public GameSnapshot Deserialize(string payload)
    {
        using var payloadDocument = JsonDocument.Parse(payload);
        var payloadRoot = payloadDocument.RootElement;

        return new GameSnapshot(
            RequiredString(payloadRoot, GameIdFieldName),
            ParsePriority(RequiredString(payloadRoot, PriorityFieldName)),
            payloadRoot.GetProperty(ItemsFieldName)
                .EnumerateArray()
                .Select(ParseItemSnapshot)
                .ToList());
    }

    private static void WriteSnapshot(Utf8JsonWriter writer, GameSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString(GameIdFieldName, snapshot.GameId);
        writer.WriteString(PriorityFieldName, snapshot.Priority.ToString());
        writer.WriteStartArray(ItemsFieldName);

        snapshot.Items.ToList().ForEach(item =>
        {
            writer.WriteStartObject();
            writer.WriteString(ItemIdFieldName, item.ItemId);
            writer.WritePropertyName(PropertiesFieldName);
            writer.WriteRawValue(item.SerializedProperties);
            writer.WriteEndObject();
        });

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static GameObjectSnapshot ParseItemSnapshot(JsonElement itemElement)
    {
        return new GameObjectSnapshot(
            RequiredString(itemElement, ItemIdFieldName),
            itemElement.GetProperty(PropertiesFieldName).GetRawText());
    }

    private static string RequiredString(JsonElement element, string fieldName)
    {
        return element.GetProperty(fieldName).GetString()
            ?? throw new ArgumentException($"Snapshot field '{fieldName}' must not be null.");
    }

    private static GamePriority ParsePriority(string priorityName)
    {
        return Enum.TryParse<GamePriority>(priorityName, out var parsedPriority)
            ? parsedPriority
            : throw new ArgumentException($"Snapshot priority '{priorityName}' is not recognized.");
    }
}
