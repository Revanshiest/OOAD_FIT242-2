using System.Text;
using System.Text.Json;
using GameServer.Games;

namespace GameServer.Resharding;

public sealed class JsonServerSnapshotCodec : ISnapshotCodec
{
    private const string GameIdFieldName = "gameId";
    private const string PriorityFieldName = "priority";
    private const string ItemsFieldName = "items";
    private const string ItemIdFieldName = "itemId";
    private const string PropertiesFieldName = "properties";
    private const string QueuedCommandsFieldName = "queuedCommands";

    public string Serialize(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var memoryStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memoryStream))
        {
            WriteSnapshot(writer, snapshot);
        }

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    public GameSnapshot Deserialize(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        using var document = JsonDocument.Parse(payload);
        var payloadRoot = document.RootElement;

        var queuedCommands = payloadRoot.TryGetProperty(QueuedCommandsFieldName, out var commandArray) && commandArray.ValueKind == JsonValueKind.Array
            ? commandArray.EnumerateArray().Select(element => element.GetString()!).ToList()
            : [];

        return new GameSnapshot(
            RequiredString(payloadRoot, GameIdFieldName),
            ParsePriority(RequiredString(payloadRoot, PriorityFieldName)),
            payloadRoot.GetProperty(ItemsFieldName)
                .EnumerateArray()
                .Select(ParseItemSnapshot)
                .ToList(),
            queuedCommands);
    }

    private static void WriteSnapshot(Utf8JsonWriter writer, GameSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString(GameIdFieldName, snapshot.GameId);
        writer.WriteString(PriorityFieldName, snapshot.Priority.ToString());
        writer.WriteStartArray(ItemsFieldName);

        foreach (var item in snapshot.Items)
        {
            writer.WriteStartObject();
            writer.WriteString(ItemIdFieldName, item.ItemId);
            writer.WritePropertyName(PropertiesFieldName);
            writer.WriteRawValue(item.SerializedProperties);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        WriteQueuedCommandsIfAny(writer, snapshot.QueuedCommands);

        writer.WriteEndObject();
    }

    private static void WriteQueuedCommandsIfAny(Utf8JsonWriter writer, IReadOnlyList<string>? commands)
    {
        while ((commands?.Count ?? 0) > 0)
        {
            writer.WriteStartArray(QueuedCommandsFieldName);
            foreach (var command in commands!)
            {
                writer.WriteStringValue(command);
            }
            writer.WriteEndArray();
            break;
        }
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

    private static GamePriority ParsePriority(string priorityString)
    {
        return Enum.Parse<GamePriority>(priorityString, ignoreCase: true);
    }
}
