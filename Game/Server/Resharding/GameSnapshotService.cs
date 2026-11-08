using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Vector = global::Game.Vector;
using Angle = global::Game.Angle;
using Game.Commands;
using GameServer.Games;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Workers;
namespace GameServer.Resharding;

using Game = GameServer.Games.Game;

public sealed class GameSnapshotService : IGameSnapshotService
{
    private static readonly ConcurrentDictionary<Type, Action<Utf8JsonWriter, object>> ValueWriters = new()
    {
        [typeof(int)] = (writer, targetObject) => writer.WriteNumberValue((int)targetObject),
        [typeof(long)] = (writer, targetObject) => writer.WriteNumberValue((long)targetObject),
        [typeof(double)] = (writer, targetObject) => writer.WriteNumberValue((double)targetObject),
        [typeof(string)] = (writer, targetObject) => writer.WriteStringValue((string)targetObject),
        [typeof(bool)] = (writer, targetObject) =>
        {
            writer.WriteStartObject();
            writer.WriteString("__type", "bool");
            writer.WriteBoolean("val", (bool)targetObject);
            writer.WriteEndObject();
        },
        [typeof(Vector)] = (writer, targetObject) =>
        {
            writer.WriteStartArray();
            foreach (var coordinate in ((Vector)targetObject).Coordinates)
            {
                writer.WriteNumberValue(coordinate);
            }
            writer.WriteEndArray();
        },
        [typeof(Angle)] = (writer, targetObject) =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("__angleNumerator", ((Angle)targetObject).Numerator);
            writer.WriteEndObject();
        }
    };

    private static readonly ConcurrentDictionary<JsonValueKind, Func<JsonElement, object>> ValueRestorers = new()
    {
        [JsonValueKind.Array] = element => new Vector(element.EnumerateArray().Select(coordinate => coordinate.GetInt32()).ToArray()),
        [JsonValueKind.Object] = RestoreObjectProperty,
        [JsonValueKind.String] = element => element.GetString() ?? throw new ArgumentException("Snapshot string property must not be null."),
        [JsonValueKind.Number] = RestoreNumberProperty
    };

    private static readonly List<(Func<JsonElement, bool> Predicate, Func<JsonElement, object> Factory)> ObjectRules =
    [
        (element => element.TryGetProperty("__angleNumerator", out _), element => new Angle(element.GetProperty("__angleNumerator").GetInt32())),
        (element => element.TryGetProperty("__type", out var typeProperty) && typeProperty.GetString() == "bool", element => element.GetProperty("val").GetBoolean())
    ];

    private static readonly Func<JsonElement, (bool Success, object Value)>[] NumberParsers =
    [
        element => (element.TryGetInt32(out var intValue), intValue),
        element => (element.TryGetInt64(out var longValue), longValue),
        element => (true, element.GetDouble())
    ];

    private static readonly Action<Utf8JsonWriter, object> DefaultStringWriter =
        (writer, propertyValue) => writer.WriteStringValue(propertyValue.ToString());

    private static readonly Dictionary<string, Func<JsonElement, Game, IScope, ICommand>> SnapshotCommandFactories = new()
    {
        [ServerCommandNames.Move] = (root, _, scope) =>
            new MoveGameItemCommand(scope, root.GetProperty("itemId").GetString()!),
        [ServerCommandNames.CreateItem] = RestoreCreateItemCommand,
        [ServerCommandNames.Suspend] = (_, game, _) =>
            new SuspendGameCommand(game, TimeProvider.System)
    };

    public static void RegisterValueWriter<T>(Action<Utf8JsonWriter, T> customWriter)
    {
        ValueWriters[typeof(T)] = (writer, targetObject) => customWriter(writer, (T)targetObject);
    }

    public static void RegisterObjectRestorer(Func<JsonElement, bool> predicate, Func<JsonElement, object> factory)
    {
        ObjectRules.Insert(0, (predicate, factory));
    }

    public GameSnapshot CaptureSnapshot(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var itemSnapshots = new List<GameObjectSnapshot>();
        foreach (var itemId in game.Scope.ItemIds)
        {
            var item = game.Scope.GetItem(itemId);
            itemSnapshots.Add(new GameObjectSnapshot(itemId, SerializeItemProperties(item)));
        }

        var queuedCommands = game.CommandQueue.GetUnprocessedCommandsSnapshot();

        return new GameSnapshot(game.GameId, game.Priority, itemSnapshots, queuedCommands);
    }

    public Game RestoreGame(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var restoredScope = new Scope();

        foreach (var itemSnapshot in snapshot.Items)
        {
            restoredScope.Register(itemSnapshot.ItemId, RestoreItem(itemSnapshot));
        }

        var restoredGame = new Game(snapshot.GameId, snapshot.Priority, restoredScope);

        foreach (var commandDescriptor in snapshot.QueuedCommands ?? [])
        {
            var command = RestoreCommand(commandDescriptor, restoredGame, restoredScope);
            restoredGame.CommandQueue.Enqueue(command);
        }

        return restoredGame;
    }

    public static ICommand RestoreCommand(string descriptor, Game targetGame, IScope targetScope)
    {
        return !string.IsNullOrWhiteSpace(descriptor) && descriptor.StartsWith("{")
            ? ParseJsonCommand(descriptor, targetGame, targetScope)
            : new EmptyCommand();
    }

    private static ICommand ParseJsonCommand(string descriptor, Game targetGame, IScope targetScope)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(descriptor);
            var root = jsonDocument.RootElement;
            var commandName = root.TryGetProperty("command", out var commandProperty) ? commandProperty.GetString() : null;

            return commandName != null && SnapshotCommandFactories.TryGetValue(commandName, out var factory)
                ? factory(root, targetGame, targetScope)
                : new EmptyCommand();
        }
        catch
        {
            return new EmptyCommand();
        }
    }

    private static ICommand RestoreCreateItemCommand(JsonElement root, Game _, IScope targetScope)
    {
        var itemId = root.GetProperty("itemId").GetString()!;
        var type = root.GetProperty("type").GetString()!;

        var positionArray = root.TryGetProperty("position", out var positionElement) && positionElement.ValueKind == JsonValueKind.Array
            ? positionElement.EnumerateArray().Select(coordinate => coordinate.GetInt32()).ToArray()
            : [0, 0];

        var speedArray = root.TryGetProperty("speed", out var speedElement) && speedElement.ValueKind == JsonValueKind.Array
            ? speedElement.EnumerateArray().Select(coordinate => coordinate.GetInt32()).ToArray()
            : [0, 0];

        var specification = new GameItemCreationSpecification(
            itemId,
            type,
            new Vector(positionArray),
            new Vector(speedArray));

        return new CreateGameItemPipeline(targetScope, specification).Build();
    }

    public static string SerializeItemProperties(IGameItem gameItem)
    {
        ArgumentNullException.ThrowIfNull(gameItem);

        using var propertiesStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(propertiesStream))
        {
            writer.WriteStartObject();

            foreach (var propertyName in gameItem.PropertyNames)
            {
                writer.WritePropertyName(propertyName);
                WritePropertyValue(writer, gameItem.GetProperty(propertyName));
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(propertiesStream.ToArray());
    }

    public static void WritePropertyValue(Utf8JsonWriter writer, object propertyValue)
    {
        var writeAction = ValueWriters.GetValueOrDefault(propertyValue.GetType(), DefaultStringWriter);
        writeAction(writer, propertyValue);
    }

    public static GameItem RestoreItem(GameObjectSnapshot itemSnapshot)
    {
        ArgumentNullException.ThrowIfNull(itemSnapshot);

        var restoredItem = new GameItem();

        using var propertiesDocument = JsonDocument.Parse(itemSnapshot.SerializedProperties);

        foreach (var property in propertiesDocument.RootElement.EnumerateObject())
        {
            restoredItem.SetProperty(property.Name, RestorePropertyValue(property.Value));
        }

        return restoredItem;
    }

    public static object RestorePropertyValue(JsonElement propertyValue)
    {
        return ValueRestorers.TryGetValue(propertyValue.ValueKind, out var restorer)
            ? restorer(propertyValue)
            : throw new ArgumentException($"Snapshot property kind '{propertyValue.ValueKind}' is not supported.");
    }

    private static object RestoreObjectProperty(JsonElement element)
    {
        var matchedRule = ObjectRules.FirstOrDefault(rule => rule.Predicate(element));

        return matchedRule != default
            ? matchedRule.Factory(element)
            : element.GetRawText();
    }

    private static object RestoreNumberProperty(JsonElement element)
    {
        return NumberParsers
            .Select(parser => parser(element))
            .First(result => result.Success)
            .Value;
    }
}
