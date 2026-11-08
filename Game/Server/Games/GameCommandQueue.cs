using System.Text.Json;
using Game.Commands;
using GameServer.Games.Pipeline;
using GameServer.Messaging;
using GameServer.Workers;

namespace GameServer.Games;

public sealed class GameCommandQueue : IGameCommandQueue
{
    private static readonly Dictionary<Type, Func<ICommand, string>> CommandSerializers = new()
    {
        [typeof(MoveGameItemCommand)] = command => JsonSerializer.Serialize(new
        {
            command = ServerCommandNames.Move,
            itemId = ((MoveGameItemCommand)command).ItemId
        }),
        [typeof(SuspendGameCommand)] = _ => JsonSerializer.Serialize(new { command = ServerCommandNames.Suspend }),
        [typeof(CreateGameItemMacroCommand)] = command => SerializeCreateItemCommand((CreateGameItemMacroCommand)command)
    };

    private readonly Queue<ICommand> _commands = new();
    private bool _isSealed;

    public int Count => _commands.Count;

    public bool HasCommands => _commands.Count > 0;

    public bool IsSealedByTickMarker => _isSealed;

    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = !_isSealed
            ? 0
            : throw new InvalidOperationException("Command queue is sealed by the tick marker and rejects client commands.");

        _commands.Enqueue(command);
    }

    public void EnqueueTickMarker(ICommand tickCommand)
    {
        ArgumentNullException.ThrowIfNull(tickCommand);

        var shouldEnqueue = !_isSealed;
        _isSealed = true;
        (shouldEnqueue ? _commands : new Queue<ICommand>()).Enqueue(tickCommand);
    }

    public IReadOnlyList<ICommand> DequeueBatch(int maxCommandCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCommandCount, 1, nameof(maxCommandCount));

        int count = Math.Min(maxCommandCount, _commands.Count);
        return Enumerable.Range(0, count).Select(_ => _commands.Dequeue()).ToArray();
    }

    public IReadOnlyList<string> GetUnprocessedCommandsSnapshot()
    {
        return _commands.Select(SerializeCommand).ToArray();
    }

    private static string SerializeCommand(ICommand command)
    {
        return CommandSerializers.TryGetValue(command.GetType(), out var serializer)
            ? serializer(command)
            : command.GetType().Name;
    }

    private static string SerializeCreateItemCommand(CreateGameItemMacroCommand macroCommand)
    {
        var specification = macroCommand.Specification;
        return JsonSerializer.Serialize(new
        {
            command = ServerCommandNames.CreateItem,
            itemId = specification.ItemId,
            type = specification.DomainType,
            position = specification.Position.Coordinates,
            speed = specification.Speed.Coordinates
        });
    }
}
