using System.Text.Json;

namespace GameServer.Endpoint;

using GameServer.Messaging;

public sealed class InboundMessageParser
{
    private const string RoutingKeyFieldName = "key";
    private const string GameIdFieldName = "id";
    private const string AuthTokenFieldName = "token";
    private const string CommandNameFieldName = "command";
    private const string CommandArgumentsFieldName = "args";

    public InboundMessage Parse(string requestPayload)
    {
        ArgumentNullException.ThrowIfNull(requestPayload);

        using var requestDocument = JsonDocument.Parse(requestPayload);
        var requestRoot = requestDocument.RootElement;

        if (requestRoot.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Request payload must be a JSON object.", nameof(requestPayload));
        }

        return new InboundMessage(
            RequiredInt32(requestRoot, RoutingKeyFieldName),
            RequiredString(requestRoot, GameIdFieldName),
            RequiredString(requestRoot, AuthTokenFieldName),
            RequiredString(requestRoot, CommandNameFieldName),
            OptionalCommandArguments(requestRoot));
    }

    private static int RequiredInt32(JsonElement requestRoot, string fieldName)
    {
        if (!requestRoot.TryGetProperty(fieldName, out var fieldElement) || fieldElement.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException($"Request field '{fieldName}' is required and must be a number.");
        }

        return fieldElement.GetInt32();
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

    private static string OptionalCommandArguments(JsonElement requestRoot)
    {
        return requestRoot.TryGetProperty(CommandArgumentsFieldName, out var argumentsElement)
            ? argumentsElement.GetRawText()
            : "{}";
    }
}
