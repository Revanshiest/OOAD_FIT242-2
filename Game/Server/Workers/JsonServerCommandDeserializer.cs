using System.Collections.Concurrent;
using System.Text.Json;

namespace GameServer.Workers;

using Game;
using Game.Commands;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using Game = GameServer.Games.Game;

public sealed class JsonServerCommandDeserializer : IServerCommandDeserializer
{
    private const string ItemIdArgumentName = "itemId";
    private const string TypeArgumentName = "type";
    private const string PositionArgumentName = "position";
    private const string SpeedArgumentName = "speed";
    public const int MaxCoordinateMagnitude = 100000;
    public const int MaxSpeedMagnitude = 10000;

    private readonly ConcurrentDictionary<string, Func<JsonElement, Game, ICommand>> _commandFactories;
    private readonly TimeProvider _timeProvider;

    public JsonServerCommandDeserializer()
        : this(TimeProvider.System, null)
    {
    }

    public JsonServerCommandDeserializer(TimeProvider timeProvider)
        : this(timeProvider, null)
    {
    }

    public JsonServerCommandDeserializer(
        TimeProvider timeProvider,
        IReadOnlyDictionary<string, Func<JsonElement, Game, ICommand>>? customFactories)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _commandFactories = new ConcurrentDictionary<string, Func<JsonElement, Game, ICommand>>
        {
            [ServerCommandNames.CreateItem] = CreateItemCommandFactory,
            [ServerCommandNames.Move] = MoveCommandFactory,
            [ServerCommandNames.Suspend] = (arguments, targetGame) => new SuspendGameCommand(targetGame, _timeProvider),
            [ServerCommandNames.Resume] = (_, _) => new EmptyCommand()
        };

        foreach (var customFactory in customFactories ?? Enumerable.Empty<KeyValuePair<string, Func<JsonElement, Game, ICommand>>>())
        {
            _commandFactories[customFactory.Key] = customFactory.Value;
        }
    }

    public void RegisterCommandFactory(string commandName, Func<JsonElement, Game, ICommand> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(factory);

        _commandFactories[commandName] = factory;
    }

    public ICommand Deserialize(InboundMessage message, Game targetGame)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetGame);

        using var argumentsDocument = JsonDocument.Parse(EffectiveArguments(message.CommandArguments));

        _ = argumentsDocument.RootElement.ValueKind == JsonValueKind.Object
            ? 0
            : throw new ArgumentException("Command arguments must be a JSON object.", nameof(message));

        var factory = _commandFactories.GetValueOrDefault(message.CommandName);
        return factory != null
            ? factory(argumentsDocument.RootElement, targetGame)
            : ResolveFallbackCommand(message, targetGame, argumentsDocument);
    }

    private static ICommand ResolveFallbackCommand(InboundMessage message, Game targetGame, JsonDocument argumentsDocument)
    {
        try
        {
            return Ioc.Resolve<ICommand>($"Server.Commands.{message.CommandName}", argumentsDocument.RootElement, targetGame);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch
        {
        }

        try
        {
            var iocFactory = Ioc.Resolve<Func<JsonElement, Game, ICommand>>($"Server.CommandFactory.{message.CommandName}");
            return iocFactory(argumentsDocument.RootElement, targetGame);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch
        {
        }

        throw new ArgumentException($"Server command '{message.CommandName}' is not registered.", nameof(message));
    }

    private static string EffectiveArguments(string commandArguments)
    {
        return string.IsNullOrWhiteSpace(commandArguments) ? "{}" : commandArguments;
    }

    private static ICommand CreateItemCommandFactory(JsonElement arguments, Game targetGame)
    {
        var specification = new GameItemCreationSpecification(
            RequiredString(arguments, ItemIdArgumentName),
            RequiredString(arguments, TypeArgumentName),
            ReadClampedVector(arguments.GetProperty(PositionArgumentName), MaxCoordinateMagnitude),
            ReadClampedVector(arguments.GetProperty(SpeedArgumentName), MaxSpeedMagnitude));

        try
        {
            return Ioc.Resolve<ICommand>("Games.CreateItem", targetGame.Scope, specification);
        }
        catch
        {
            return new CreateGameItemPipeline(targetGame.Scope, specification).Build();
        }
    }

    private static ICommand MoveCommandFactory(JsonElement arguments, Game targetGame)
    {
        return new MoveGameItemCommand(targetGame.Scope, RequiredString(arguments, ItemIdArgumentName));
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new ArgumentException($"Argument '{propertyName}' must be a string.");
    }

    private static Vector ReadClampedVector(JsonElement arrayElement, int maxMagnitude)
    {
        return new Vector(arrayElement
            .EnumerateArray()
            .Select(element => Math.Clamp(element.GetInt32(), -maxMagnitude, maxMagnitude))
            .ToArray());
    }
}
