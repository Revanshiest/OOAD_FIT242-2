using Game;
using Game.Commands;
using Game.IoC;
using GameServer.IoC;
using GameServer.Messaging;
using GameServer.Routing;
using GameServer.Resharding;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Routing;

public class PrimaryRouterTests
{
    private readonly List<RecordingMessageWriter> _recordingWriters =
    [
        new RecordingMessageWriter(),
        new RecordingMessageWriter()
    ];

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenWriterListIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new PrimaryRouter([], new ReshardingTable(1)));
    }

    [Fact]
    public void ResolveTarget_UsesHashDispatch_WhenNoReshardingOverrideIsRegistered()
    {
        var primaryRouter = CreateRouter();

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(1, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[1], routeTarget.Writer);
    }

    [Fact]
    public void ResolveTarget_PrefersReshardingOverride_OverHashDispatch()
    {
        var reshardingTable = new ReshardingTable(2);
        var primaryRouter = new PrimaryRouter(_recordingWriters.ToList(), reshardingTable);
        reshardingTable.SetRoute("game-1", 0);

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(0, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[0], routeTarget.Writer);
    }

    [Fact]
    public void ResolveTarget_ReturnsToHashDispatch_AfterOverrideRemoval()
    {
        var reshardingTable = new ReshardingTable(2);
        var primaryRouter = new PrimaryRouter(_recordingWriters.ToList(), reshardingTable);
        reshardingTable.SetRoute("game-1", 0);
        reshardingTable.RemoveRoute("game-1");

        var routeTarget = primaryRouter.ResolveTarget(5, "game-1");

        Assert.Equal(1, routeTarget.ThreadId);
        Assert.Same(_recordingWriters[1], routeTarget.Writer);
    }

    [Fact]
    public void PrimaryRouter_GuaranteesConsistentSessionAffinity_PreventingDuplicateGamesAcrossWorkers()
    {
        var firstWorkerQueue = new RecordingMessageWriter();
        var secondWorkerQueue = new RecordingMessageWriter();
        var reshardingTable = new ReshardingTable(2);
        var primaryRouter = new PrimaryRouter([firstWorkerQueue, secondWorkerQueue], reshardingTable);

        var target1 = primaryRouter.ResolveTarget(0, "game-100");
        Assert.Equal(0, target1.ThreadId);
        Assert.Same(firstWorkerQueue, target1.Writer);

        var target2 = primaryRouter.ResolveTarget(1, "game-100");
        Assert.Equal(0, target2.ThreadId);
        Assert.Same(firstWorkerQueue, target2.Writer);

        var target3 = primaryRouter.ResolveTarget(999, "game-100");
        Assert.Equal(0, target3.ThreadId);
        Assert.Same(firstWorkerQueue, target3.Writer);
    }

    [Fact]
    public void PrimaryRouter_RegisterWorker_DynamicallyExpandsRouterAndDispatchesToNewWorker()
    {
        var initialWorker = new RecordingMessageWriter();
        var reshardingTable = new ReshardingTable(1);
        var primaryRouter = new PrimaryRouter([initialWorker], reshardingTable);

        Assert.Equal(1, primaryRouter.WorkerCount);

        var newWorker = new RecordingMessageWriter();
        primaryRouter.RegisterWorker(1, newWorker);

        Assert.Equal(2, primaryRouter.WorkerCount);
        Assert.Equal(2, reshardingTable.ThreadCount);

        var targetForThread1 = primaryRouter.ResolveTarget(1, "game-new-2");
        Assert.Equal(1, targetForThread1.ThreadId);
        Assert.Same(newWorker, targetForThread1.Writer);
    }

    [Fact]
    public void PrimaryRouter_Create_ResolvesViaServerRouterCreateStrategy_WhenRegisteredInIoC()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

        new RegisterIoCDependencyServerRouter().Execute();

        var writers = new List<IMessageQueueWriter<InboundMessage>>
        {
            new RecordingMessageWriter(),
            new RecordingMessageWriter()
        };

        var router = PrimaryRouter.Create(writers, 2);
        Assert.NotNull(router);
        Assert.Equal(2, router.WorkerCount);

        var target = router.ResolveTarget(1, "game-1");
        Assert.Equal(1, target.ThreadId);
    }

    [Fact]
    public void PrimaryRouter_WorksWithIReshardingTable_Abstraction()
    {
        var mockTable = new MockReshardingTable();
        var writer = new RecordingMessageWriter();
        var router = new PrimaryRouter([writer], mockTable);

        var target = router.ResolveTarget(0, "game-res");

        Assert.Equal(0, target.ThreadId);
        Assert.True(mockTable.ResolveRouteCalled);
        Assert.True(mockTable.SetRouteCalled);
    }

    [Fact]
    public void PrimaryRouter_ReturnsCachedTarget_OnOverriddenRoute()
    {
        var writer1 = new RecordingMessageWriter();
        var writer2 = new RecordingMessageWriter();
        var reshardingTable = new ReshardingTable(2);
        var router = new PrimaryRouter([writer1, writer2], reshardingTable);

        reshardingTable.SetRoute("game-1", 1);

        var targetFirst = router.ResolveTarget(0, "game-1");
        var targetSecond = router.ResolveTarget(0, "game-1");

        Assert.Same(targetFirst, targetSecond);
        Assert.Equal(1, targetFirst.ThreadId);
    }

    private sealed class MockReshardingTable : IReshardingTable
    {
        public int ThreadCount => 1;
        public bool ResolveRouteCalled { get; private set; }
        public bool SetRouteCalled { get; private set; }

        public void UpdateThreadCount(int threadCount) { }
        public void SetRoute(string gameId, int threadId) => SetRouteCalled = true;
        public void RemoveRoute(string gameId) { }
        public void RemoveRoutesForThread(int threadId) { }
        public int? ResolveRoute(string gameId)
        {
            ResolveRouteCalled = true;
            return null;
        }
    }

    private PrimaryRouter CreateRouter()
    {
        return new PrimaryRouter(_recordingWriters.ToList(), new ReshardingTable(2));
    }
}
