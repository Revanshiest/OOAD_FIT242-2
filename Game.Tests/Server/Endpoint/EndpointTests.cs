using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.Endpoints;

public class EndpointTests
{
    private const string ValidAuthToken = "secret";
    private const int WorkerCount = 2;

    private readonly List<RecordingMessageWriter> _recordingWriters =
    [
        new RecordingMessageWriter(),
        new RecordingMessageWriter()
    ];

    [Fact]
    public void Accept_RoutesValidMessageIntoHashTargetQueue_AndReturnsActualRoutingKey()
    {
        var endpoint = CreateEndpoint();
        const string requestPayload = """{"key": 3, "id": "game-1", "token": "secret", "command": "Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.True(endpointResponse.Accepted);
        Assert.Null(endpointResponse.RejectionReason);
        Assert.Equal(1, endpointResponse.RoutingKey);
        Assert.Single(_recordingWriters[1].WrittenMessages);
        Assert.Empty(_recordingWriters[0].WrittenMessages);
    }

    [Fact]
    public void Accept_RejectsMalformedJsonPayload()
    {
        var endpoint = CreateEndpoint();

        var endpointResponse = endpoint.Accept("not-a-json");

        Assert.False(endpointResponse.Accepted);
        Assert.Equal(EndpointRejectionReason.MalformedMessage, endpointResponse.RejectionReason);
    }

    [Fact]
    public void Accept_RejectsPayloadWithMissingRoutingField()
    {
        var endpoint = CreateEndpoint();
        const string requestPayload = """{"id": "game-1", "token": "secret", "command": "Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.False(endpointResponse.Accepted);
        Assert.Equal(EndpointRejectionReason.MalformedMessage, endpointResponse.RejectionReason);
    }

    [Fact]
    public void Accept_RejectsMessageWithUnknownAuthToken()
    {
        var endpoint = CreateEndpoint();
        const string requestPayload = """{"key": 0, "id": "game-1", "token": "intruder", "command": "Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.False(endpointResponse.Accepted);
        Assert.Equal(EndpointRejectionReason.AuthenticationFailed, endpointResponse.RejectionReason);
        Assert.All(_recordingWriters, writer => Assert.Empty(writer.WrittenMessages));
    }

    [Fact]
    public void Accept_RejectsMessage_WhenTargetQueueIsFull()
    {
        var boundedQueue = new BoundedMessageQueue<InboundMessage>(1);
        var endpoint = CreateEndpoint([boundedQueue]);
        const string firstRequest = """{"key": 0, "id": "game-1", "token": "secret", "command": "Move"}""";
        const string secondRequest = """{"key": 0, "id": "game-2", "token": "secret", "command": "Move"}""";

        var firstResponse = endpoint.Accept(firstRequest);
        var secondResponse = endpoint.Accept(secondRequest);

        Assert.True(firstResponse.Accepted);
        Assert.False(secondResponse.Accepted);
        Assert.Equal(EndpointRejectionReason.WorkerQueueFull, secondResponse.RejectionReason);
    }

    [Fact]
    public void Accept_ReturnsReshardedRoutingKey_AfterRouteOverrideIsRegistered()
    {
        var reshardingTable = new ReshardingTable(WorkerCount);
        var endpoint = new Endpoint(
            new WhitelistTokenAuthenticator([ValidAuthToken]),
            new InboundMessageParser(),
            new PrimaryRouter(_recordingWriters.ToList(), reshardingTable));
        reshardingTable.SetRoute("game-1", 0);
        const string requestPayload = """{"key": 1, "id": "game-1", "token": "secret", "command": "Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.True(endpointResponse.Accepted);
        Assert.Equal(0, endpointResponse.RoutingKey);
        Assert.Single(_recordingWriters[0].WrittenMessages);
    }

    [Fact]
    public void Accept_RejectsNewIncomingRequestsImmediately_WhenAcceptanceSealed()
    {
        var endpoint = CreateEndpoint();
        const string validPayload = """{"key": 0, "id": "game-1", "token": "secret", "command": "Move"}""";

        var responseBeforeSeal = endpoint.Accept(validPayload);
        Assert.True(responseBeforeSeal.Accepted);

        endpoint.SealAcceptance();
        Assert.True(endpoint.IsAcceptanceSealed);

        var responseAfterSeal = endpoint.Accept(validPayload);
        Assert.False(responseAfterSeal.Accepted);
        Assert.Equal(EndpointRejectionReason.ServerStopping, responseAfterSeal.RejectionReason);

        endpoint.ResumeAcceptance();
        Assert.False(endpoint.IsAcceptanceSealed);

        var responseAfterResume = endpoint.Accept(validPayload);
        Assert.True(responseAfterResume.Accepted);
    }

    [Fact]
    public void Accept_UsesCachedResponses_OnAccept()
    {
        var endpoint = CreateEndpoint();
        const string validPayload = """{"key": 0, "id": "game-1", "token": "secret", "command": "Move"}""";

        var res1 = endpoint.Accept(validPayload);
        var res2 = endpoint.Accept(validPayload);

        Assert.True(res1.Accepted);
        Assert.Same(res1, res2);

        var malformedRes1 = endpoint.Accept("invalid-json");
        var malformedRes2 = endpoint.Accept("invalid-json");
        Assert.False(malformedRes1.Accepted);
        Assert.Same(malformedRes1, malformedRes2);
    }

    [Fact]
    public void Accept_WorksWithAbstractions_AndDependencyInversion()
    {
        var mockRouter = new MockRouter();
        var mockParser = new MockParser();
        var authenticator = new WhitelistTokenAuthenticator([ValidAuthToken]);

        var endpoint = new Endpoint(authenticator, mockParser, mockRouter);

        var response = endpoint.Accept("test-payload");
        Assert.True(response.Accepted);
        Assert.True(mockParser.ParseCalled);
        Assert.True(mockRouter.ResolveTargetCalled);
    }

    private Endpoint CreateEndpoint()
    {
        return CreateEndpoint(_recordingWriters.ToList());
    }

    private Endpoint CreateEndpoint(IReadOnlyList<IMessageQueueWriter<InboundMessage>> targetWriters)
    {
        return new Endpoint(
            new WhitelistTokenAuthenticator([ValidAuthToken]),
            new InboundMessageParser(),
            new PrimaryRouter(targetWriters, new ReshardingTable(Math.Max(targetWriters.Count, 1))));
    }

    private sealed class MockRouter : IRouter
    {
        public bool ResolveTargetCalled { get; private set; }
        private readonly RecordingMessageWriter _writer = new();

        public RouteTarget ResolveTarget(int routingKey, string gameId)
        {
            ResolveTargetCalled = true;
            return new RouteTarget(0, _writer);
        }
    }

    private sealed class MockParser : IMessageParser<InboundMessage>
    {
        public bool ParseCalled { get; private set; }

        public InboundMessage Parse(string requestPayload)
        {
            ParseCalled = true;
            return new InboundMessage(0, "game-mock", ValidAuthToken, "Move", "{}");
        }
    }
}
