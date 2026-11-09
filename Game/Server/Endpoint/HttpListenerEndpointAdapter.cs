using System.Collections.Frozen;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GameServer.Endpoint;

public sealed class HttpListenerEndpointAdapter : IDisposable
{
    private const int MaximumPortNumber = 65535;
    private const string HealthStatusJson = """{"status":"running"}""";
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IEndpoint _endpoint;
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, Action<HttpListenerContext>> _methodHandlers;
    private int _isStopped;

    private static readonly FrozenDictionary<EndpointRejectionReason, HttpStatusCode> RejectionStatusCodes =
        new Dictionary<EndpointRejectionReason, HttpStatusCode>
        {
            [EndpointRejectionReason.ServerStopping] = HttpStatusCode.ServiceUnavailable,
            [EndpointRejectionReason.WorkerQueueFull] = HttpStatusCode.UnprocessableEntity,
            [EndpointRejectionReason.AuthenticationFailed] = HttpStatusCode.UnprocessableEntity,
            [EndpointRejectionReason.MalformedMessage] = HttpStatusCode.UnprocessableEntity,
            [EndpointRejectionReason.RateLimited] = HttpStatusCode.UnprocessableEntity,
            [EndpointRejectionReason.PayloadTooLarge] = HttpStatusCode.UnprocessableEntity,
            [EndpointRejectionReason.ForbiddenCommand] = HttpStatusCode.Forbidden,
            [EndpointRejectionReason.ServerUnavailable] = HttpStatusCode.ServiceUnavailable
        }.ToFrozenDictionary();

    public HttpListenerEndpointAdapter(IEndpoint endpoint, int port)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1, nameof(port));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, MaximumPortNumber, nameof(port));

        _endpoint = endpoint;
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        _methodHandlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GET"] = HandleGetRequest,
            ["POST"] = HandleAcceptRequest
        };
    }

    public int Port { get; }

    public bool IsStopped => Volatile.Read(ref _isStopped) == 1;

    public void Start()
    {
        _listener.Start();
        var acceptLoopThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = $"endpoint-http-{Port}"
        };
        acceptLoopThread.Start();
    }

    public void Stop()
    {
        var shouldClose = Interlocked.Exchange(ref _isStopped, 1) == 0;
        Action closeAction = shouldClose ? CloseListenerSafely : delegate { };
        closeAction();
    }

    private void CloseListenerSafely()
    {
        try
        {
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void AcceptLoop()
    {
        while (!IsStopped && _listener.IsListening)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    var httpContext = (HttpListenerContext)state!;
                    try
                    {
                        HandleRequest(httpContext);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            httpContext.Response.Close();
                        }
                        catch
                        {
                        }
                    }
                }, context);
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var handler = _methodHandlers.GetValueOrDefault(context.Request.HttpMethod, DefaultMethodNotAllowedHandler);
        handler(context);
    }

    private static void DefaultMethodNotAllowedHandler(HttpListenerContext context)
    {
        WriteJsonResponse(context, HttpStatusCode.MethodNotAllowed, "{}");
    }

    private void HandleGetRequest(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        var hasCommandParam = !string.IsNullOrEmpty(query["command"]) || !string.IsNullOrEmpty(query["id"]);

        var getRoute = hasCommandParam
            ? HandleGetWithQueryParameters
            : (context.Request.HasEntityBody ? (Action<HttpListenerContext>)HandleGetWithEntityBody : HandleGetHealthStatus);

        getRoute(context);
    }

    private void HandleGetWithQueryParameters(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        var key = int.TryParse(query["key"], out var parsedKey) ? parsedKey : 0;
        var id = query["id"] ?? string.Empty;
        var token = query["token"] ?? string.Empty;
        var command = query["command"] ?? string.Empty;
        var rawArguments = query["args"] ?? "{}";

        var payloadDict = new Dictionary<string, object>
        {
            ["key"] = key,
            ["id"] = id,
            ["command"] = command,
            ["token"] = token
        };

        try
        {
            using var argumentsDocument = JsonDocument.Parse(rawArguments);
            payloadDict["args"] = argumentsDocument.RootElement.Clone();
        }
        catch
        {
        }

        var requestPayload = JsonSerializer.Serialize(payloadDict);
        ProcessAndRespond(context, requestPayload);
    }

    private void HandleGetWithEntityBody(HttpListenerContext context)
    {
        using var bodyReader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = bodyReader.ReadToEnd();
        var isValidJson = !string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{");

        Action<HttpListenerContext> action = isValidJson
            ? httpContext => ProcessAndRespond(httpContext, body)
            : HandleGetHealthStatus;

        action(context);
    }

    private static void HandleGetHealthStatus(HttpListenerContext context)
    {
        WriteJsonResponse(context, HttpStatusCode.OK, HealthStatusJson);
    }

    private void HandleAcceptRequest(HttpListenerContext context)
    {
        using var bodyReader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var requestPayload = bodyReader.ReadToEnd();
        ProcessAndRespond(context, requestPayload);
    }

    private void ProcessAndRespond(HttpListenerContext context, string requestPayload)
    {
        var endpointResponse = _endpoint.Accept(requestPayload);
        var responseStatusCode = endpointResponse.Accepted
            ? HttpStatusCode.OK
            : RejectionStatusCodes.GetValueOrDefault(
                endpointResponse.RejectionReason!.Value,
                HttpStatusCode.UnprocessableEntity);

        WriteJsonResponse(context, responseStatusCode, JsonSerializer.Serialize(endpointResponse, ResponseSerializerOptions));
    }

    private static void WriteJsonResponse(HttpListenerContext context, HttpStatusCode statusCode, string jsonResponse)
    {
        try
        {
            var responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Flush();
        }
        catch (Exception)
        {
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }
    }
}
