namespace Game.Tests.Server.Load;

using System.Diagnostics;
using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Scaling;
using GameServer.Workers;
using Game.Tests.Server.Resharding;

public static class ServerLoadTestHarness
{
    public const int GameCount = 100;
    public const int ProducerCount = 4;
    public const int MessagesPerProducer = 5000;
    public const int ProcessingCompletionTimeoutMilliseconds = 60000;
    private const int MessageAcceptRetryTimeoutMilliseconds = 5000;

    public static GameWorkerSettings CreateLoadTestSettings()
    {
        return new GameWorkerSettings(
            64,
            4096,
            4096,
            TimeSpan.FromMinutes(5),
            1000000,
            1000000);
    }

    public static async Task<LoadRunResult> RunLoadAsync(
        Func<int, Endpoint> endpointFactory,
        CountingServerCommandDeserializer countingDeserializer,
        WorkerPool workerPool)
    {
        var totalMessageCount = ProducerCount * MessagesPerProducer;
        var loadStopwatch = Stopwatch.StartNew();

        var producers = Enumerable
            .Range(0, ProducerCount)
            .Select(producerIndex => Task.Run(() => ProduceMessages(endpointFactory(producerIndex), producerIndex)))
            .ToArray();

        await Task.WhenAll(producers);

        var allMessagesProcessed = SpinWait.SpinUntil(
            () => countingDeserializer.DeserializedCount >= totalMessageCount,
            ProcessingCompletionTimeoutMilliseconds);
        loadStopwatch.Stop();

        workerPool.StopAll();

        if (!allMessagesProcessed)
        {
            throw new TimeoutException($"Only {countingDeserializer.DeserializedCount} of {totalMessageCount} messages were processed within the timeout.");
        }

        var elapsedTotalSeconds = loadStopwatch.Elapsed.TotalSeconds;

        return new LoadRunResult(
            totalMessageCount,
            countingDeserializer.DeserializedCount,
            elapsedTotalSeconds,
            totalMessageCount / elapsedTotalSeconds);
    }

    private static void ProduceMessages(Endpoint endpoint, int producerIndex)
    {
        Enumerable
            .Range(0, MessagesPerProducer)
            .ToList()
            .ForEach(sequenceNumber =>
            {
                var requestPayload = BuildRequestPayload(producerIndex, sequenceNumber);
                var messageAccepted = SpinWait.SpinUntil(
                    () => endpoint.Accept(requestPayload).Accepted,
                    MessageAcceptRetryTimeoutMilliseconds);

                if (!messageAccepted)
                {
                    throw new TimeoutException($"Producer {producerIndex} failed to deliver message {sequenceNumber} within the timeout.");
                }
            });
    }

    private static string BuildRequestPayload(int producerIndex, int sequenceNumber)
    {
        var gameNumber = (producerIndex * MessagesPerProducer + sequenceNumber) % GameCount;

        return string.Concat(
            "{\"key\":", producerIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ",\"id\":\"game-", gameNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "\",\"token\":\"secret\",\"command\":\"Move\",\"args\":{\"itemId\":\"ship1\"}}");
    }
}
