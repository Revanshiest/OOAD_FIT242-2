using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Scaling;
using GameServer.Workers;
using Game.Tests.Server.Resharding;
using Xunit.Abstractions;

namespace Game.Tests.Server.Load;

public class ServerLoadTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _testOutput;

    public ServerLoadTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [Fact]
    public async Task LoadConfiguration1_SingleWorker_ProcessesAllMessages()
    {
        var workerPool = CreateWorkerPool(out var countingDeserializer);
        workerPool.StartWorker();
        var endpoint = CreateEndpoint([workerPool.ResolveWriter(0)]);

        var loadRunResult = await ServerLoadTestHarness.RunLoadAsync(_ => endpoint, countingDeserializer, workerPool);

        ReportMetrics("single-worker", loadRunResult);
        Assert.Equal(loadRunResult.SentMessages, loadRunResult.ProcessedMessages);
    }

    [Fact]
    public async Task LoadConfiguration2_SharedRouterAcrossTwoWorkers_ProcessesAllMessages()
    {
        var workerPool = CreateWorkerPool(out var countingDeserializer);
        workerPool.StartWorker();
        workerPool.StartWorker();
        var sharedRouter = new PrimaryRouter(
            [workerPool.ResolveWriter(0), workerPool.ResolveWriter(1)],
            new ReshardingTable(2));
        var sharedEndpoint = new Endpoint(
            new WhitelistTokenAuthenticator(["secret"]),
            new InboundMessageParser(),
            sharedRouter);

        var loadRunResult = await ServerLoadTestHarness.RunLoadAsync(_ => sharedEndpoint, countingDeserializer, workerPool);

        ReportMetrics("two-workers-shared-router", loadRunResult);
        Assert.Equal(loadRunResult.SentMessages, loadRunResult.ProcessedMessages);
        Assert.True(workerPool.CollectStatistics().All(statistics => statistics.GameCount > 0));
    }

    [Fact]
    public async Task LoadConfiguration2_IsolatedRouterPerWorker_ProcessesAllMessages()
    {
        var workerPool = CreateWorkerPool(out var countingDeserializer);
        workerPool.StartWorker();
        workerPool.StartWorker();
        var isolatedEndpoints = new List<Endpoint>
        {
            CreateEndpoint([workerPool.ResolveWriter(0)]),
            CreateEndpoint([workerPool.ResolveWriter(1)])
        };

        var loadRunResult = await ServerLoadTestHarness.RunLoadAsync(
            producerIndex => isolatedEndpoints[producerIndex % isolatedEndpoints.Count],
            countingDeserializer,
            workerPool);

        ReportMetrics("two-workers-isolated-routers", loadRunResult);
        Assert.Equal(loadRunResult.SentMessages, loadRunResult.ProcessedMessages);
    }

    private void ReportMetrics(string configurationName, LoadRunResult loadRunResult)
    {
        _testOutput.WriteLine(
            $"config={configurationName} messages={loadRunResult.SentMessages} "
            + $"elapsed_sec={loadRunResult.ElapsedTotalSeconds:F3} "
            + $"messages_per_sec={loadRunResult.ProcessedMessagesPerSecond:F0}");
    }

    private static Endpoint CreateEndpoint(IReadOnlyList<IMessageQueueWriter<InboundMessage>> targetWriters)
    {
        return new Endpoint(
            new WhitelistTokenAuthenticator(["secret"]),
            new InboundMessageParser(),
            new PrimaryRouter(targetWriters, new ReshardingTable(Math.Max(targetWriters.Count, 1))));
    }

    private static WorkerPool CreateWorkerPool(out CountingServerCommandDeserializer countingDeserializer)
    {
        countingDeserializer = new CountingServerCommandDeserializer(
            new JsonServerCommandDeserializer(new FixedTimeProvider(TestTimestamp)));

        return new WorkerPool(
            ServerLoadTestHarness.CreateLoadTestSettings(),
            new FixedTimeProvider(TestTimestamp),
            NullGameTerminationObserver.Instance,
            countingDeserializer,
            new DefaultGameFactory(),
            new JsonServerSnapshotCodec(),
            new BackupTransitThread(),
            new InMemorySnapshotStore());
    }
}
