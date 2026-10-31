using System.Net;
using System.Text;
using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Routing;
using Game.Tests.Server;

namespace Game.Tests.Server.Endpoints;

public class HttpListenerEndpointAdapterIntegrationTests
{
    [Fact]
    public async Task PostRequest_AcceptsMessage_AndRespondsWithActualRoutingKey()
    {
        var allocatedPort = TestPortAllocator.GetFreePort();
        var firstTargetQueue = new BoundedMessageQueue<InboundMessage>(16);
        var secondTargetQueue = new BoundedMessageQueue<InboundMessage>(16);
        var endpointAdapter = CreateEndpointAdapter([firstTargetQueue, secondTargetQueue], allocatedPort);

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{allocatedPort}/") };
        var requestContent = new StringContent(
            """{"key": 1, "id": "game-1", "token": "secret", "command": "Move", "args": {"itemId": "ship1"}}""",
            Encoding.UTF8,
            "application/json");

        var httpResponse = await httpClient.PostAsync(string.Empty, requestContent);
        var responsePayload = await httpResponse.Content.ReadAsStringAsync();

        endpointAdapter.Stop();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Contains("\"accepted\":true", responsePayload);
        Assert.Contains("\"routingKey\":1", responsePayload);
        Assert.Equal(1, secondTargetQueue.PendingCount);
        Assert.Equal(0, firstTargetQueue.PendingCount);
    }

    [Fact]
    public async Task PostRequest_WithUnknownToken_IsRejectedWithUnprocessableEntity()
    {
        var allocatedPort = TestPortAllocator.GetFreePort();
        var targetQueue = new BoundedMessageQueue<InboundMessage>(16);
        var endpointAdapter = CreateEndpointAdapter([targetQueue], allocatedPort);

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{allocatedPort}/") };
        var requestContent = new StringContent(
            """{"key": 0, "id": "game-1", "token": "intruder", "command": "Move"}""",
            Encoding.UTF8,
            "application/json");

        var httpResponse = await httpClient.PostAsync(string.Empty, requestContent);

        endpointAdapter.Stop();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, httpResponse.StatusCode);
        Assert.Equal(0, targetQueue.PendingCount);
    }

    [Fact]
    public async Task GetRequest_RespondsWithHealthStatus()
    {
        var allocatedPort = TestPortAllocator.GetFreePort();
        var targetQueue = new BoundedMessageQueue<InboundMessage>(16);
        var endpointAdapter = CreateEndpointAdapter([targetQueue], allocatedPort);

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{allocatedPort}/") };

        var httpResponse = await httpClient.GetAsync(string.Empty);
        var responsePayload = await httpResponse.Content.ReadAsStringAsync();

        endpointAdapter.Stop();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Contains("running", responsePayload);
    }

    private static HttpListenerEndpointAdapter CreateEndpointAdapter(IReadOnlyList<IMessageQueueWriter<InboundMessage>> targetWriters, int port)
    {
        var endpoint = new Endpoint(
            new WhitelistTokenAuthenticator(["secret"]),
            new InboundMessageParser(),
            new PrimaryRouter(targetWriters, new ReshardingTable(targetWriters.Count)));
        var httpAdapter = new HttpListenerEndpointAdapter(endpoint, port);
        httpAdapter.Start();

        return httpAdapter;
    }
}
