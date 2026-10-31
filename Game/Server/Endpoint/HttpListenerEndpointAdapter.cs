using System.Net;
using System.Text;
using System.Text.Json;

namespace GameServer.Endpoint;

public sealed class HttpListenerEndpointAdapter : IDisposable
{
    private const string HealthStatusJson = """{"status":"running"}""";
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Endpoint _endpoint;
    private readonly HttpListener _listener = new();
    private bool _isStopped;

    public HttpListenerEndpointAdapter(Endpoint endpoint, int port)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be within [1, 65535].");
        }

        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public int Port { get; }

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
        if (_isStopped)
        {
            return;
        }

        _isStopped = true;
        _listener.Stop();
        _listener.Close();
    }

    public void Dispose()
    {
        Stop();
    }

    private void AcceptLoop()
    {
        try
        {
            while (_listener.IsListening)
            {
                HandleRequest(_listener.GetContext());
            }
        }
        catch (Exception exception) when (!_listener.IsListening)
        {
            _ = exception;
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        switch (context.Request.HttpMethod)
        {
            case "GET":
                WriteJsonResponse(context, HttpStatusCode.OK, HealthStatusJson);
                break;
            case "POST":
                HandleAcceptRequest(context);
                break;
            default:
                WriteJsonResponse(context, HttpStatusCode.MethodNotAllowed, "{}");
                break;
        }
    }

    private void HandleAcceptRequest(HttpListenerContext context)
    {
        using var bodyReader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var requestPayload = bodyReader.ReadToEnd();
        var endpointResponse = _endpoint.Accept(requestPayload);
        var responseStatusCode = endpointResponse.Accepted ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity;

        WriteJsonResponse(context, responseStatusCode, JsonSerializer.Serialize(endpointResponse, ResponseSerializerOptions));
    }

    private static void WriteJsonResponse(HttpListenerContext context, HttpStatusCode statusCode, string jsonResponse)
    {
        var responseBytes = Encoding.UTF8.GetBytes(jsonResponse);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = responseBytes.Length;
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        context.Response.OutputStream.Close();
    }
}
