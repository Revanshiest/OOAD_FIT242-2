namespace GameServer.Endpoint;

using System.Collections.Frozen;
using System.Text.Json;
using GameServer.Messaging;
using GameServer.Routing;

public enum EndpointRejectionReason
{
    MalformedMessage = 0,
    AuthenticationFailed = 1,
    WorkerQueueFull = 2,
    RateLimited = 3,
    PayloadTooLarge = 4,
    ServerStopping = 5,
    ForbiddenCommand = 6,
    ServerUnavailable = 7
}

public sealed record EndpointResponse(bool Accepted, int RoutingKey, EndpointRejectionReason? RejectionReason);

public sealed class PayloadTooLargeException : ArgumentException
{
    public PayloadTooLargeException(string message, string paramName)
        : base(message, paramName)
    {
    }
}

public sealed class Endpoint : IEndpoint
{
    private static readonly EndpointResponse StoppingResponse = new(false, 0, EndpointRejectionReason.ServerStopping);
    private static readonly EndpointResponse PayloadTooLargeResponse = new(false, 0, EndpointRejectionReason.PayloadTooLarge);
    private static readonly EndpointResponse MalformedMessageResponse = new(false, 0, EndpointRejectionReason.MalformedMessage);
    private static readonly EndpointResponse AuthFailedResponse = new(false, 0, EndpointRejectionReason.AuthenticationFailed);
    private static readonly EndpointResponse RateLimitedResponse = new(false, 0, EndpointRejectionReason.RateLimited);
    private static readonly EndpointResponse QueueFullResponse = new(false, 0, EndpointRejectionReason.WorkerQueueFull);
    private static readonly EndpointResponse ForbiddenCommandResponse = new(false, 0, EndpointRejectionReason.ForbiddenCommand);
    private static readonly EndpointResponse ServerUnavailableResponse = new(false, 0, EndpointRejectionReason.ServerUnavailable);

    private static readonly FrozenDictionary<EndpointRejectionReason, EndpointResponse> RejectionResponses =
        new Dictionary<EndpointRejectionReason, EndpointResponse>
        {
            [EndpointRejectionReason.ServerStopping] = StoppingResponse,
            [EndpointRejectionReason.PayloadTooLarge] = PayloadTooLargeResponse,
            [EndpointRejectionReason.MalformedMessage] = MalformedMessageResponse,
            [EndpointRejectionReason.AuthenticationFailed] = AuthFailedResponse,
            [EndpointRejectionReason.RateLimited] = RateLimitedResponse,
            [EndpointRejectionReason.WorkerQueueFull] = QueueFullResponse,
            [EndpointRejectionReason.ForbiddenCommand] = ForbiddenCommandResponse,
            [EndpointRejectionReason.ServerUnavailable] = ServerUnavailableResponse
        }.ToFrozenDictionary();

    private const int PrecomputedResponsePoolSize = 64;

    private static readonly EndpointResponse[] PrecomputedAcceptedResponses =
        Enumerable.Range(0, PrecomputedResponsePoolSize).Select(threadId => new EndpointResponse(true, threadId, null)).ToArray();

    private static readonly Dictionary<bool, Func<int, EndpointResponse>> WorkerDispatchOutcomes = new()
    {
        [true] = GetAcceptedResponse,
        [false] = threadId => new EndpointResponse(false, threadId, EndpointRejectionReason.WorkerQueueFull)
    };

    private static readonly Dictionary<(bool Auth, bool Rate), Func<Endpoint, InboundMessage, EndpointResponse>> InboundValidations = new()
    {
        [(false, false)] = (_, _) => Rejected(EndpointRejectionReason.AuthenticationFailed),
        [(false, true)] = (_, _) => Rejected(EndpointRejectionReason.AuthenticationFailed),
        [(true, false)] = (_, _) => Rejected(EndpointRejectionReason.RateLimited),
        [(true, true)] = (endpoint, message) => endpoint.DispatchToWorker(message)
    };

    private readonly IAuthenticator _authenticator;
    private readonly IMessageParser<InboundMessage> _messageParser;
    private readonly IRouter _router;
    private readonly IGameRateLimiter _gameRateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<bool, Func<string, EndpointResponse>> _acceptanceRoutes;
    private volatile bool _isSealed;

    public static EndpointResponse GetAcceptedResponse(int threadId)
    {
        return PrecomputedAcceptedResponses.ElementAtOrDefault(threadId)
            ?? new EndpointResponse(true, threadId, null);
    }

    public Endpoint(IAuthenticator authenticator, IMessageParser<InboundMessage> messageParser, IRouter router)
        : this(authenticator, messageParser, router, PermissiveRateLimiter.Instance, TimeProvider.System)
    {
    }

    public Endpoint(
        IAuthenticator authenticator,
        IMessageParser<InboundMessage> messageParser,
        IRouter router,
        IGameRateLimiter gameRateLimiter,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(messageParser);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(gameRateLimiter);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _authenticator = authenticator;
        _messageParser = messageParser;
        _router = router;
        _gameRateLimiter = gameRateLimiter;
        _timeProvider = timeProvider;
        _acceptanceRoutes = new()
        {
            [true] = _ => StoppingResponse,
            [false] = AcceptActive
        };
    }

    public bool IsAcceptanceSealed => _isSealed;

    public void SealAcceptance()
    {
        _isSealed = true;
    }

    public void ResumeAcceptance()
    {
        _isSealed = false;
    }

    public EndpointResponse Accept(string requestPayload)
    {
        return _acceptanceRoutes[_isSealed](requestPayload);
    }

    internal EndpointResponse AcceptActive(string requestPayload)
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

        var isAuth = _authenticator.IsAuthenticated(inboundMessage);
        var isInternalCommand = inboundMessage.CommandName.StartsWith("Server.", StringComparison.Ordinal);
        var isRateAdmitted = isAuth && !isInternalCommand
            && _gameRateLimiter.TryAdmit(inboundMessage.GameId, _timeProvider.GetUtcNow());

        return isAuth && isInternalCommand
            ? Rejected(EndpointRejectionReason.ForbiddenCommand)
            : InboundValidations[(isAuth, isRateAdmitted)](this, inboundMessage);
    }

    private EndpointResponse DispatchToWorker(InboundMessage inboundMessage)
    {
        try
        {
            var routeTarget = _router.ResolveTarget(inboundMessage.RoutingKey, inboundMessage.GameId);
            var isWritten = routeTarget.Writer.TryWrite(inboundMessage);
            return WorkerDispatchOutcomes[isWritten](routeTarget.ThreadId);
        }
        catch (NoWorkersAvailableException)
        {
            return Rejected(EndpointRejectionReason.ServerUnavailable);
        }
    }

    private static EndpointResponse Rejected(EndpointRejectionReason rejectionReason)
    {
        return RejectionResponses.GetValueOrDefault(rejectionReason, new EndpointResponse(false, 0, rejectionReason));
    }
}
