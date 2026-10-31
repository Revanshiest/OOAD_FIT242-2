using System.Text.Json;

namespace GameServer.Workers;

using GameServer.Messaging;
using Game = GameServer.Games.Game;

public sealed record WorkerCycleResult(
    int RoutedMessageCount,
    int RejectedMessageCount,
    int DispatchedCommandCount,
    int FailedCommandCount,
    IReadOnlyList<string> TerminatedGameIds);

public sealed class GameWorker
{
    private readonly IMessageQueueReader<InboundMessage> _inputReader;
    private readonly IGameScheduler _scheduler;
    private readonly IServerCommandDeserializer _commandDeserializer;
    private readonly IGameFactory _gameFactory;
    private readonly GameWorkerSettings _settings;

    public GameWorker(
        IMessageQueueReader<InboundMessage> inputReader,
        IGameScheduler scheduler,
        IServerCommandDeserializer commandDeserializer,
        IGameFactory gameFactory,
        GameWorkerSettings settings)
    {
        _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _commandDeserializer = commandDeserializer ?? throw new ArgumentNullException(nameof(commandDeserializer));
        _gameFactory = gameFactory ?? throw new ArgumentNullException(nameof(gameFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public WorkerCycleResult ExecuteCycle(DateTimeOffset currentTime)
    {
        return RouteAndDispatch(_inputReader.DrainAllAvailable(), currentTime);
    }

    public WorkerCycleResult ExecuteBlockingCycle(DateTimeOffset currentTime, CancellationToken cancellationToken)
    {
        var incomingMessages = _inputReader.DrainAllAvailable();

        if (incomingMessages.Count == 0)
        {
            incomingMessages = new[] { _inputReader.ReadBlocking(cancellationToken) };
        }

        return RouteAndDispatch(incomingMessages, currentTime);
    }

    private WorkerCycleResult RouteAndDispatch(IReadOnlyList<InboundMessage> incomingMessages, DateTimeOffset currentTime)
    {
        var routingResults = incomingMessages.Select(RouteOneMessage).ToList();
        var scheduleOutcome = _scheduler.ScheduleCycle(currentTime);

        return new WorkerCycleResult(
            routingResults.Count(accepted => accepted),
            routingResults.Count(accepted => !accepted),
            scheduleOutcome.DispatchedCommandCount,
            scheduleOutcome.FailedCommandCount,
            scheduleOutcome.TerminatedGameIds);
    }

    private bool RouteOneMessage(InboundMessage message)
    {
        var targetGame = _scheduler.FindGame(message.GameId);

        if (targetGame is null)
        {
            targetGame = CreateGameWithinLimit(message.GameId);

            if (targetGame is null)
            {
                return false;
            }
        }

        if (message.CommandName == ServerCommandNames.Resume && targetGame.IsAwaitingExternalEvent)
        {
            targetGame.ResumeFromExternalEvent();
        }

        if (targetGame.CommandQueue.Count >= _settings.MaxQueueLengthPerGame)
        {
            _scheduler.Unregister(targetGame.GameId, GameTerminationReason.FloodQueueOverflow);
            return false;
        }

        try
        {
            var command = _commandDeserializer.Deserialize(message, targetGame);
            targetGame.CommandQueue.Enqueue(command);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }

        _scheduler.NotifyCommandsQueued(targetGame);
        return true;
    }

    private Game? CreateGameWithinLimit(string gameId)
    {
        if (_scheduler.RegisteredGameCount >= _settings.MaxActiveGames)
        {
            return null;
        }

        var createdGame = _gameFactory.Create(gameId);
        _scheduler.Register(createdGame);

        return createdGame;
    }
}
