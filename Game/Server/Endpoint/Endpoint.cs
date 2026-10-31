using System.Text.Json;

namespace GameServer.Endpoint;

using GameServer.Messaging;
using GameServer.Routing;

public enum EndpointRejectionReason
{
    MalformedMessage = 0,
    AuthenticationFailed = 1,
    WorkerQueueFull = 2
}

public sealed record EndpointResponse(bool Accepted, int RoutingKey, EndpointRejectionReason? RejectionReason);

public sealed class Endpoint
{
    private readonly IAuthenticator _authenticator;
    private readonly InboundMessageParser _messageParser;
    private readonly PrimaryRouter _router;

    public Endpoint(IAuthenticator authenticator, InboundMessageParser messageParser, PrimaryRouter router)
    {
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    public EndpointResponse Accept(string requestPayload)
    {
        InboundMessage inboundMessage;

        try
        {
            inboundMessage = _messageParser.Parse(requestPayload);
        }
        catch (JsonException)
        {
            return Rejected(EndpointRejectionReason.MalformedMessage);
        }
        catch (ArgumentException)
        {
            return Rejected(EndpointRejectionReason.MalformedMessage);
        }

        if (!_authenticator.IsAuthenticated(inboundMessage.AuthToken))
        {
            return Rejected(EndpointRejectionReason.AuthenticationFailed);
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
