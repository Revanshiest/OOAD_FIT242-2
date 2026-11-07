using App;
using Game;
using Game.Commands;
using GameServer.Endpoint;
using GameServer.Games;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Scaling;
using GameServer.Workers;
using ServerGame = GameServer.Games.Game;

var now = DateTimeOffset.UtcNow;
var checks = 0;
void Observed(string name, bool defect)
{
    Console.WriteLine($"{name}: defect_observed={defect}");
    if (!defect) throw new Exception($"Unexpected result: {name}");
    checks++;
}
InboundMessage Msg(string command, string args = "{}", string id = "g") => new(0, id, "token", command, args);
InboundMessage Create(string id = "g") => Msg(ServerCommandNames.CreateItem,
    """{"itemId":"ship","type":"Ship","position":[0,0],"speed":[1,0]}""", id);
InboundMessage Move() => Msg(ServerCommandNames.Move, """{"itemId":"ship"}""");
var routes = new Dictionary<int, IMessageQueueWriter<InboundMessage>>();
Cell Make(int id, int maxGames = 100, int quantum = 8)
{
    var queue = new BoundedMessageQueue<InboundMessage>(64);
    var settings = new GameWorkerSettings(quantum, 256, maxGames, TimeSpan.FromMinutes(5), 1000, 1000);
    var scheduler = new PriorityRoundRobinScheduler(settings, new CommandRateLimiter(1000,1000), NullGameTerminationObserver.Instance);
    var store = new InMemorySnapshotStore();
    var worker = new GameWorker(queue, scheduler, new JsonServerCommandDeserializer(TimeProvider.System),
        new DefaultGameFactory(), settings, new GameWorkerSupportServices(new JsonServerSnapshotCodec(), new Resolver(routes), new OverflowSink(), store));
    routes[id] = queue;
    return new Cell(queue, scheduler, worker, store);
}

{
    routes.Clear(); var source = Make(0); var target = Make(1);
    source.Queue.TryWrite(Create()); source.Worker.ExecuteCycle(now);
    var table = new ReshardingTable(2);
    new GameMigrationCoordinator(table, new Poster(routes)).BeginThreadMigration(new("g",0,1));
    var endpoint = new Endpoint(new WhitelistTokenAuthenticator(["token"]), new InboundMessageParser(), new PrimaryRouter([source.Queue,target.Queue],table));
    var response = endpoint.Accept("""{"key":0,"id":"g","token":"token","command":"Move","args":{"itemId":"ship"}}""");
    target.Worker.ExecuteCycle(now);
    source.Worker.ExecuteCycle(now);
    var result = target.Worker.ExecuteCycle(now);
    Observed("migration_route_before_snapshot", response.Accepted && result.RejectedMessageCount == 1 && !target.Scheduler.FindGame("g")!.Scope.Contains("ship"));
}
{
    routes.Clear(); var source = Make(0);
    source.Queue.TryWrite(Create()); source.Worker.ExecuteCycle(now);
    source.Queue.TryWrite(Msg(ServerCommandNames.MigrateOut,"""{"targetThreadId":9}""")); source.Worker.ExecuteCycle(now);
    var target = Make(9); source.Worker.ExecuteCycle(now);
    Observed("missing_target_deletes_source_game", source.Scheduler.FindGame("g") is null && target.Queue.PendingCount == 0);
}
{
    routes.Clear(); var source = Make(0); Make(1);
    source.Queue.TryWrite(Create()); source.Worker.ExecuteCycle(now);
    source.Queue.TryWrite(Msg(ServerCommandNames.MigrateOut,"""{"targetThreadId":1}""")); source.Queue.TryWrite(Move());
    var result = source.Worker.ExecuteCycle(now);
    Observed("accepted_message_after_migration_tick_is_dropped", result.RejectedMessageCount == 1);
}
{
    routes.Clear(); var cell = Make(0);
    cell.Queue.TryWrite(Create()); cell.Queue.TryWrite(Msg(ServerCommandNames.Suspend)); cell.Queue.TryWrite(Move());
    cell.Worker.ExecuteCycle(now);
    var game = cell.Scheduler.FindGame("g")!;
    Observed("suspend_does_not_stop_current_batch", game.IsAwaitingExternalEvent && ((Vector)game.Scope.GetItem("ship").GetProperty("Position")).Coordinates[0] == 1);
}
{
    routes.Clear(); var cell = Make(0);
    cell.Queue.TryWrite(Msg(ServerCommandNames.Suspend)); cell.Worker.ExecuteCycle(now);
    cell.Worker.ExecuteCycle(now.AddMinutes(6));
    cell.Queue.TryWrite(Create()); var result = cell.Worker.ExecuteCycle(now.AddMinutes(6));
    Observed("watchdog_leaves_unreachable_recipient", cell.Scheduler.FindGame("g") is null && result.RoutedMessageCount == 1 && result.DispatchedCommandCount == 0);
}
{
    routes.Clear(); var cell = Make(0); var threw = false;
    cell.Queue.TryWrite(Msg(ServerCommandNames.CreateItem,"""{"itemId":42}""")); cell.Queue.TryWrite(Create("good"));
    try { cell.Worker.ExecuteCycle(now); } catch(InvalidOperationException) { threw = true; }
    cell.Worker.ExecuteCycle(now);
    Observed("malformed_args_discards_rest_of_drained_batch", threw && cell.Queue.PendingCount == 0 && cell.Scheduler.FindGame("good") is null);
}
{
    routes.Clear(); var cell = Make(0);
    var endpoint = new Endpoint(new WhitelistTokenAuthenticator(["token"]),new InboundMessageParser(),new PrimaryRouter([cell.Queue],new ReshardingTable(1)));
    var response = endpoint.Accept("""{"key":0,"id":"client","token":"token","command":"Server.Tick","args":{}}""");
    cell.Worker.ExecuteCycle(now);
    cell.Queue.TryWrite(Create()); var result = cell.Worker.ExecuteCycle(now);
    Observed("client_can_seal_entire_worker", response.Accepted && cell.Worker.IsShutdownCompleted && result.RejectedMessageCount == 1);
}
{
    routes.Clear(); var cell = Make(0,1);
    cell.Queue.TryWrite(Create()); cell.Worker.ExecuteCycle(now);
    cell.Queue.TryWrite(Msg(ServerCommandNames.MigrateIn,"""{"gameId":"g2","priority":"Normal","items":[]}"""));
    cell.Worker.ExecuteCycle(now);
    Observed("migration_bypasses_game_limit",cell.Scheduler.RegisteredGameCount == 2);
}
{
    routes.Clear(); var source = Make(0); var target = Make(1);
    source.Queue.TryWrite(Create()); source.Worker.ExecuteCycle(now);
    source.Scheduler.FindGame("g")!.Scope.GetItem("ship").SetProperty("Alive",true);
    source.Queue.TryWrite(Msg(ServerCommandNames.MigrateOut,"""{"targetThreadId":1}""")); source.Worker.ExecuteCycle(now); target.Worker.ExecuteCycle(now);
    Observed("snapshot_changes_boolean_to_string", target.Scheduler.FindGame("g")!.Scope.GetItem("ship").GetProperty("Alive") is string);
}
{
    routes.Clear(); var cell = Make(0);
    cell.Queue.TryWrite(Msg(ServerCommandNames.Suspend)); cell.Queue.TryWrite(Msg(ServerCommandNames.Resume));
    cell.Worker.ExecuteCycle(now);
    Observed("resume_overtakes_suspend",cell.Scheduler.FindGame("g")!.IsAwaitingExternalEvent);
}
{
    routes.Clear(); var cell = Make(0);
    var oldCount = 0; var newCount = 0;
    var oldGame = new ServerGame("same",GamePriority.Normal,new Scope()); oldGame.CommandQueue.Enqueue(new ActionCommand(()=>oldCount++));
    cell.Scheduler.Register(oldGame); cell.Scheduler.Unregister("same",GameTerminationReason.FloodQueueOverflow);
    var newGame = new ServerGame("same",GamePriority.Normal,new Scope()); newGame.CommandQueue.Enqueue(new ActionCommand(()=>newCount++));
    cell.Scheduler.Register(newGame); cell.Scheduler.ScheduleCycle(now);
    Observed("removed_game_commands_run_after_same_id_recreated",oldCount == 1 && newCount == 1);
}
{
    var auth = new HmacMessageAuthenticator("secret",TimeProvider.System); var threw = false;
    var timestamp = unchecked(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - long.MinValue);
    try { auth.IsAuthenticated(Msg("Move") with { AuthToken = $"{timestamp}.00" }); } catch(OverflowException) { threw = true; }
    Observed("unsigned_timestamp_crashes_authenticator",threw);
}
{
    var table = new ReshardingTable(2);
    try { new GameMigrationCoordinator(table,new RejectingPoster()).BeginThreadMigration(new("g",0,1)); }
    catch(InvalidOperationException) { }
    Observed("failed_migration_leaves_route_switched",table.ResolveRoute("g") == 1);
}
{
    routes.Clear(); var left = Make(0); var right = Make(1);
    var router = new PrimaryRouter([left.Queue,right.Queue],new ReshardingTable(2));
    router.ResolveTarget(0,"g").Writer.TryWrite(Create());
    router.ResolveTarget(1,"g").Writer.TryWrite(Create() with { RoutingKey = 1 });
    left.Worker.ExecuteCycle(now); right.Worker.ExecuteCycle(now);
    Observed("same_game_id_can_exist_on_two_workers",left.Scheduler.FindGame("g") is not null && right.Scheduler.FindGame("g") is not null);
}
{
    var target = new BoundedMessageQueue<InboundMessage>(1); target.TryWrite(Move());
    var backup = new BackupTransitThread(1); backup.Start(); backup.Overflow(Move(),target);
    if (!SpinWait.SpinUntil(()=>target.PendingCount == 2,2000)) throw new Exception("Backup did not reach blocked write");
    backup.Stop(); var join = Task.Run(backup.Join); var blocked = !join.Wait(100);
    target.DrainAllAvailable(); if (!join.Wait(2000)) throw new Exception("Backup cleanup timed out");
    Observed("backup_stop_cannot_cancel_target_write",blocked);
}
{
    using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    var pool = new WorkerPool(GameWorkerSettings.CreateDefault(),TimeProvider.System,NullGameTerminationObserver.Instance,
        new BlockingDeserializer(entered,release),new DefaultGameFactory(),new JsonServerSnapshotCodec(),new OverflowSink(),new InMemorySnapshotStore());
    pool.StartWorker(); pool.TryPost(0,Move());
    if (!entered.Wait(2000)) throw new Exception("Command did not start");
    var stop = Task.Run(pool.StopAll); var blocked = !stop.Wait(100); release.Set();
    if (!stop.Wait(2000)) throw new Exception("Worker cleanup timed out");
    Observed("hard_stop_waits_for_running_command",blocked);
}
Console.WriteLine($"reproduced_defects={checks}");

record Cell(BoundedMessageQueue<InboundMessage> Queue, PriorityRoundRobinScheduler Scheduler, GameWorker Worker, InMemorySnapshotStore Store);
sealed class Resolver(Dictionary<int,IMessageQueueWriter<InboundMessage>> routes) : ITransitResolver
{ public IMessageQueueWriter<InboundMessage> ResolveWriter(int threadId) => routes[threadId]; }
sealed class Poster(Dictionary<int,IMessageQueueWriter<InboundMessage>> routes) : IWorkerMessagePoster
{ public bool TryPost(int threadId, InboundMessage message) => routes[threadId].TryWrite(message); }
sealed class OverflowSink : ITransitOverflowSink
{ public void Overflow(InboundMessage message,IMessageQueueWriter<InboundMessage> targetWriter) => throw new Exception("Unexpected overflow in sequential check"); }
sealed class ActionCommand(Action action) : ICommand { public void Execute() => action(); }
sealed class RejectingPoster : IWorkerMessagePoster { public bool TryPost(int threadId,InboundMessage message) => false; }
sealed class BlockingDeserializer(ManualResetEventSlim entered,ManualResetEventSlim release) : IServerCommandDeserializer
{ public ICommand Deserialize(InboundMessage message,ServerGame game) => new ActionCommand(()=> { entered.Set(); release.Wait(); }); }
