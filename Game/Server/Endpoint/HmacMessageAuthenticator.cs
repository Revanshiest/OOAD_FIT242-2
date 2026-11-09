using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameServer.Endpoint;

using GameServer.Messaging;

public sealed class HmacMessageAuthenticator : IAuthenticator
{
    public const int SignatureTimestampWindowSeconds = 300;
    private const int SignatureCleanupThreshold = 1000;
    private const char TokenSeparator = '.';
    private const char CanonicalFieldSeparator = '|';
    private readonly string _signingSecret;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, long> _seenSignatures = new();

    public HmacMessageAuthenticator(string signingSecret, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingSecret);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _signingSecret = signingSecret;
        _timeProvider = timeProvider;
    }

    public bool IsAuthenticated(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return !string.IsNullOrWhiteSpace(message.AuthToken) && ValidateTokenStructure(message);
    }

    private bool ValidateTokenStructure(InboundMessage message)
    {
        var token = message.AuthToken;
        var separatorIndex = token.LastIndexOf(TokenSeparator);
        var isValidSeparator = separatorIndex >= 1 && separatorIndex < token.Length - 1;

        return isValidSeparator && ValidateTokenSignature(token[..separatorIndex], token[(separatorIndex + 1)..], message);
    }

    private bool ValidateTokenSignature(string timestampPart, string signaturePart, InboundMessage message)
    {
        var canParse = long.TryParse(timestampPart, CultureInfo.InvariantCulture, out var messageTimestampUnixSeconds);
        var isSignatureValid = canParse
            && IsTimestampWithinWindow(messageTimestampUnixSeconds)
            && IsSignatureValid(timestampPart, signaturePart, message);

        return isSignatureValid && RecordSignature(signaturePart, message, messageTimestampUnixSeconds);
    }

    private bool RecordSignature(string signaturePart, InboundMessage message, long messageTimestampUnixSeconds)
    {
        var currentUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        PruneExpiredTokens(currentUnixSeconds);

        var tokenKey = $"{signaturePart}:{message.MessageId}";
        return _seenSignatures.TryAdd(tokenKey, messageTimestampUnixSeconds);
    }

    private void PruneExpiredTokens(long currentUnixSeconds)
    {
        var expiredKeys = _seenSignatures
            .Where(signatureEntry => _seenSignatures.Count > SignatureCleanupThreshold && Math.Abs(currentUnixSeconds - signatureEntry.Value) > SignatureTimestampWindowSeconds)
            .Select(signatureEntry => signatureEntry.Key)
            .ToList();

        foreach (var signatureKey in expiredKeys)
        {
            _seenSignatures.TryRemove(signatureKey, out _);
        }
    }

    public string Sign(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var timestampUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var timestampPart = timestampUnixSeconds.ToString(CultureInfo.InvariantCulture);

        return string.Concat(timestampPart, TokenSeparator, ComputeSignature(timestampPart, message));
    }

    private bool IsTimestampWithinWindow(long messageTimestampUnixSeconds)
    {
        var currentUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        return messageTimestampUnixSeconds >= currentUnixSeconds - SignatureTimestampWindowSeconds
            && messageTimestampUnixSeconds <= currentUnixSeconds + SignatureTimestampWindowSeconds;
    }

    private bool IsSignatureValid(string timestampPart, string signaturePart, InboundMessage message)
    {
        return signaturePart.Length > 0
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signaturePart),
                Encoding.UTF8.GetBytes(ComputeSignature(timestampPart, message)));
    }

    private string ComputeSignature(string timestampPart, InboundMessage message)
    {
        var canonicalPayload = string.Concat(
            message.RoutingKey.ToString(CultureInfo.InvariantCulture),
            CanonicalFieldSeparator,
            message.GameId,
            CanonicalFieldSeparator,
            message.CommandName,
            CanonicalFieldSeparator,
            message.CommandArguments,
            CanonicalFieldSeparator,
            message.MessageId ?? string.Empty,
            CanonicalFieldSeparator,
            timestampPart);

        var secretBytes = Encoding.UTF8.GetBytes(_signingSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var computedHash = HMACSHA256.HashData(secretBytes, payloadBytes);

        return Convert.ToHexStringLower(computedHash);
    }
}
