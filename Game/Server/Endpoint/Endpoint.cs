using System.Text.Json;

namespace GameServer.Endpoint;

using GameServer.Messaging;
using GameServer.Resharding;
using GameServer.Routing;

public enum EndpointRejectionReason
{
    MalformedMessage = 0,
    AuthenticationFailed = 1,
    WorkerQueueFull = 2,
    RateLimited = 3,
    PayloadTooLarge = 4
}

public sealed record EndpointResponse(bool Accepted, int RoutingKey, EndpointRejectionReason? RejectionReason);

public sealed class PayloadTooLargeException : ArgumentException
{
    public PayloadTooLargeException(string message, string paramName)
        : base(message, paramName)
    {
    }
}

public sealed class Endpoint
{
    private readonly IAuthenticator _authenticator;
    private readonly InboundMessageParser _messageParser;
    private readonly PrimaryRouter _router;
    private readonly IGameRateLimiter _gameRateLimiter;
    private readonly TimeProvider _timeProvider;

    public Endpoint(IAuthenticator authenticator, InboundMessageParser messageParser, PrimaryRouter router)
        : this(authenticator, messageParser, router, PermissiveRateLimiter.Instance, TimeProvider.System)
    {
    }

    public Endpoint(
        IAuthenticator authenticator,
        InboundMessageParser messageParser,
        PrimaryRouter router,
        IGameRateLimiter gameRateLimiter,
        TimeProvider timeProvider)
    {
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _gameRateLimiter = gameRateLimiter ?? throw new ArgumentNullException(nameof(gameRateLimiter));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public EndpointResponse Accept(string requestPayload)
    {
        InboundMessage inboundMessage;

        try
        {
            inboundMessage = _messageParser.Parse(requestPayload);
        }
        catch (PayloadTooLargeException)
        {
            return Rejected(EndpointRejectionReason.PayloadTooLarge);
        }
        catch (JsonException)
        {
            return Rejected(EndpointRejectionReason.MalformedMessage);
        }
        catch (ArgumentException)
        {
            return Rejected(EndpointRejectionReason.MalformedMessage);
        }

        if (!_authenticator.IsAuthenticated(inboundMessage))
        {
            return Rejected(EndpointRejectionReason.AuthenticationFailed);
        }

        if (!_gameRateLimiter.TryAdmit(inboundMessage.GameId, _timeProvider.GetUtcNow()))
        {
            return Rejected(EndpointRejectionReason.RateLimited);
        }

        var routeTarget = _router.ResolveTarget(inboundMessage.RoutingKey, inboundMessage.GameId);

        return routeTarget.Writer.TryWrite(inboundMessage)
            ? new EndpointResponse(true, routeTarget.ThreadId, null)
            : Rejected(EndpointRejectionReason.WorkerQueueFull);
    }

    private static EndpointResponse Rejected(EndpointRejectionReason rejectionReason)
    {
        return new EndpointResponse(false, 0, rejectionReason);
    }
}
