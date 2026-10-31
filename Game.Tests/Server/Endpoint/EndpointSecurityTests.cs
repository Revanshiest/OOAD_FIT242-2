using System.Text;
using GameServer.Endpoint;
using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;
using GameServer.Workers;
using Game.Tests.Server;

namespace Game.Tests.Server.Endpoints;

public class EndpointSecurityTests
{
    private const string ValidAuthToken = "secret";
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FixedTimeProvider _timeProvider = new(TestTimestamp);

    [Fact]
    public void Accept_RejectsOversizedPayload()
    {
        var endpoint = CreatePermissiveEndpoint();
        var oversizedPayload = "{\"key\":0,\"id\":\"g\",\"token\":\"secret\",\"command\":\"Move\",\"args\":{\"filler\":\""
            + new string('x', InboundMessageParser.MaxPayloadLengthBytes) + "\"}}";

        var endpointResponse = endpoint.Accept(oversizedPayload);

        Assert.Equal(EndpointRejectionReason.PayloadTooLarge, endpointResponse.RejectionReason);
    }

    [Fact]
    public void Accept_AcceptsPayloadAtExactSizeBoundary()
    {
        var endpoint = CreatePermissiveEndpoint();
        var payloadTemplate = "{\"key\":0,\"id\":\"game-1\",\"token\":\"secret\",\"command\":\"Move\",\"args\":{\"filler\":\"";
        var fillerLength = InboundMessageParser.MaxPayloadLengthBytes - Encoding.UTF8.GetByteCount(payloadTemplate) - 3;
        var boundaryPayload = payloadTemplate + new string('x', fillerLength) + "\"}}";

        var endpointResponse = endpoint.Accept(boundaryPayload);

        Assert.NotEqual(EndpointRejectionReason.PayloadTooLarge, endpointResponse.RejectionReason);
        Assert.True(endpointResponse.Accepted);
    }

    [Fact]
    public void Accept_RejectsIdentifierOneCharacterBeyondLengthLimit()
    {
        var endpoint = CreatePermissiveEndpoint();
        var longGameId = new string('g', InboundMessageParser.MaxIdentifierLength + 1);
        var requestPayload = $$"""{"key":0,"id":"{{longGameId}}","token":"secret","command":"Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.Equal(EndpointRejectionReason.MalformedMessage, endpointResponse.RejectionReason);
    }

    [Fact]
    public void Accept_AcceptsIdentifierAtExactLengthLimit()
    {
        var endpoint = CreatePermissiveEndpoint();
        var limitGameId = new string('g', InboundMessageParser.MaxIdentifierLength);
        var requestPayload = $$"""{"key":0,"id":"{{limitGameId}}","token":"secret","command":"Move"}""";

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.True(endpointResponse.Accepted);
    }

    [Fact]
    public void Accept_RateLimitsGame_BeyondTokenBucketCapacity_BeforeRouting()
    {
        var rateLimiter = new CommandRateLimiter(1, 0);
        var endpoint = CreateEndpointWithRateLimiter(rateLimiter);
        var firstResponse = endpoint.Accept(BuildMovePayload("game-1"));
        var secondResponse = endpoint.Accept(BuildMovePayload("game-1"));
        var thirdResponse = endpoint.Accept(BuildMovePayload("game-2"));

        Assert.True(firstResponse.Accepted);
        Assert.Equal(EndpointRejectionReason.RateLimited, secondResponse.RejectionReason);
        Assert.True(thirdResponse.Accepted);
    }

    [Fact]
    public void Accept_RateLimitRecovers_AfterRefillElapsedTime()
    {
        var rateLimiter = new CommandRateLimiter(1, 1);
        var endpoint = CreateEndpointWithRateLimiter(rateLimiter);

        Assert.True(endpoint.Accept(BuildMovePayload("game-1")).Accepted);
        Assert.Equal(EndpointRejectionReason.RateLimited, endpoint.Accept(BuildMovePayload("game-1")).RejectionReason);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.True(endpoint.Accept(BuildMovePayload("game-1")).Accepted);
    }

    [Fact]
    public void Accept_AcceptsCommandWithMissingArgumentsField_ForWorkerLevelValidation()
    {
        var targetQueue = new BoundedMessageQueue<InboundMessage>(16);
        var endpoint = CreatePermissiveEndpoint([targetQueue]);

        var endpointResponse = endpoint.Accept("""{"key":0,"id":"game-1","token":"secret","command":"Move"}""");

        Assert.True(endpointResponse.Accepted);
        Assert.Equal(1, targetQueue.PendingCount);
    }

    [Fact]
    public void Accept_RejectsDeeplyNestedArgumentsDepthBomb()
    {
        var endpoint = CreatePermissiveEndpoint();
        var depthBombArguments = string.Concat(Enumerable.Repeat("[", 200)) + string.Concat(Enumerable.Repeat("]", 200));
        var requestPayload = string.Concat(
            "{\"key\":0,\"id\":\"game-1\",\"token\":\"secret\",\"command\":\"Move\",\"args\":",
            depthBombArguments, "}");

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.Equal(EndpointRejectionReason.MalformedMessage, endpointResponse.RejectionReason);
    }

    [Fact]
    public void Accept_AcceptsArgumentsWithinDefaultJsonDepthLimit()
    {
        var endpoint = CreatePermissiveEndpoint();
        var shallowArguments = string.Concat(Enumerable.Repeat("[", 30)) + string.Concat(Enumerable.Repeat("]", 30));
        var requestPayload = string.Concat(
            "{\"key\":0,\"id\":\"game-1\",\"token\":\"secret\",\"command\":\"Move\",\"args\":",
            shallowArguments, "}");

        var endpointResponse = endpoint.Accept(requestPayload);

        Assert.True(endpointResponse.Accepted);
    }

    private Endpoint CreatePermissiveEndpoint(IReadOnlyList<IMessageQueueWriter<InboundMessage>>? targetWriters = null)
    {
        var writers = targetWriters ?? [new BoundedMessageQueue<InboundMessage>(16)];

        return new Endpoint(
            new WhitelistTokenAuthenticator([ValidAuthToken]),
            new InboundMessageParser(),
            new PrimaryRouter(writers, new ReshardingTable(Math.Max(writers.Count, 1))),
            PermissiveRateLimiter.Instance,
            _timeProvider);
    }

    private Endpoint CreateEndpointWithRateLimiter(CommandRateLimiter rateLimiter)
    {
        var writers = new List<IMessageQueueWriter<InboundMessage>>
        {
            new BoundedMessageQueue<InboundMessage>(64)
        };

        return new Endpoint(
            new WhitelistTokenAuthenticator([ValidAuthToken]),
            new InboundMessageParser(),
            new PrimaryRouter(writers, new ReshardingTable(1)),
            rateLimiter,
            _timeProvider);
    }

    private static string BuildMovePayload(string gameId)
    {
        return string.Concat(
            "{\"key\":0,\"id\":\"", gameId,
            "\",\"token\":\"secret\",\"command\":\"Move\",\"args\":{\"itemId\":\"ship1\"}}");
    }
}
