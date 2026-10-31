using System.Collections.Frozen;
using System.Text.Json;

namespace GameServer.Workers;

using Game;
using Game.Commands;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using Game = GameServer.Games.Game;

public sealed class JsonServerCommandDeserializer(TimeProvider timeProvider) : IServerCommandDeserializer
{
    private const string ItemIdArgumentName = "itemId";
    private const string TypeArgumentName = "type";
    private const string PositionArgumentName = "position";
    private const string SpeedArgumentName = "speed";

    private readonly FrozenDictionary<string, Func<JsonElement, Game, ICommand>> _commandFactories =
        new Dictionary<string, Func<JsonElement, Game, ICommand>>
        {
            [ServerCommandNames.CreateItem] = CreateItemCommandFactory,
            [ServerCommandNames.Move] = MoveCommandFactory,
            [ServerCommandNames.Suspend] = (arguments, targetGame) => new SuspendGameCommand(targetGame, timeProvider),
            [ServerCommandNames.Resume] = (_, _) => new EmptyCommand()
        }.ToFrozenDictionary();

    public ICommand Deserialize(InboundMessage message, Game targetGame)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetGame);

        using var argumentsDocument = JsonDocument.Parse(EffectiveArguments(message.CommandArguments));

        return _commandFactories.GetValueOrDefault(message.CommandName)
            ?.Invoke(argumentsDocument.RootElement, targetGame)
            ?? throw new ArgumentException($"Server command '{message.CommandName}' is not registered.", nameof(message));
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
            ReadVector(arguments.GetProperty(PositionArgumentName)),
            ReadVector(arguments.GetProperty(SpeedArgumentName)));

        return new CreateGameItemPipeline(targetGame.Scope, specification).Build();
    }

    private static ICommand MoveCommandFactory(JsonElement arguments, Game targetGame)
    {
        return new MoveGameItemCommand(targetGame.Scope, RequiredString(arguments, ItemIdArgumentName));
    }

    private static Vector ReadVector(JsonElement vectorElement)
    {
        return new Vector(vectorElement.EnumerateArray().Select(component => component.GetInt32()).ToArray());
    }

    private static string RequiredString(JsonElement arguments, string argumentName)
    {
        return arguments.GetProperty(argumentName).GetString()
            ?? throw new ArgumentException($"Command argument '{argumentName}' is required.");
    }
}
