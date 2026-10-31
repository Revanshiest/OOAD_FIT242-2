using System.Text;
using System.Text.Json;

namespace GameServer.Endpoint;

using GameServer.Messaging;

public sealed class InboundMessageParser
{
    public const int MaxPayloadLengthBytes = 65536;
    public const int MaxIdentifierLength = 64;
    private const string RoutingKeyFieldName = "key";
    private const string GameIdFieldName = "id";
    private const string AuthTokenFieldName = "token";
    private const string CommandNameFieldName = "command";
    private const string CommandArgumentsFieldName = "args";

    public InboundMessage Parse(string requestPayload)
    {
        ArgumentNullException.ThrowIfNull(requestPayload);

        if (Encoding.UTF8.GetByteCount(requestPayload) > MaxPayloadLengthBytes)
        {
            throw new PayloadTooLargeException(
                $"Request payload exceeds the maximum size of {MaxPayloadLengthBytes} bytes.",
                nameof(requestPayload));
        }

        using var requestDocument = JsonDocument.Parse(requestPayload);
        var requestRoot = requestDocument.RootElement;

        if (requestRoot.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Request payload must be a JSON object.", nameof(requestPayload));
        }

        return new InboundMessage(
            RequiredInt32(requestRoot, RoutingKeyFieldName),
            RequiredIdentifier(requestRoot, GameIdFieldName),
            RequiredString(requestRoot, AuthTokenFieldName),
            RequiredIdentifier(requestRoot, CommandNameFieldName),
            OptionalCommandArguments(requestRoot));
    }

    private static int RequiredInt32(JsonElement requestRoot, string fieldName)
    {
        if (!requestRoot.TryGetProperty(fieldName, out var fieldElement) || fieldElement.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException($"Request field '{fieldName}' is required and must be a number.");
        }

        return fieldElement.TryGetInt32(out var routingKey)
            ? routingKey
            : throw new ArgumentException($"Request field '{fieldName}' must fit into a 32-bit integer.");
    }

    private static string RequiredString(JsonElement requestRoot, string fieldName)
    {
        if (!requestRoot.TryGetProperty(fieldName, out var fieldElement) || fieldElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Request field '{fieldName}' is required and must be a string.");
        }

        return fieldElement.GetString()
            ?? throw new ArgumentException($"Request field '{fieldName}' must not be null.");
    }

    private static string RequiredIdentifier(JsonElement requestRoot, string fieldName)
    {
        var identifierValue = RequiredString(requestRoot, fieldName);

        return identifierValue.Length > 0 && identifierValue.Length <= MaxIdentifierLength
            ? identifierValue
            : throw new ArgumentException(
                $"Request field '{fieldName}' length must be within [1, {MaxIdentifierLength}].");
    }

    private static string OptionalCommandArguments(JsonElement requestRoot)
    {
        return requestRoot.TryGetProperty(CommandArgumentsFieldName, out var argumentsElement)
            ? argumentsElement.GetRawText()
            : "{}";
    }
}
