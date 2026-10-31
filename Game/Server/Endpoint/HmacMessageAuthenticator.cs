using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameServer.Endpoint;

using GameServer.Messaging;

public sealed class HmacMessageAuthenticator : IAuthenticator
{
    public const int SignatureTimestampWindowSeconds = 300;
    private const char TokenSeparator = '.';
    private const char CanonicalFieldSeparator = '|';
    private readonly string _signingSecret;
    private readonly TimeProvider _timeProvider;

    public HmacMessageAuthenticator(string signingSecret, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new ArgumentException("Signing secret is required.", nameof(signingSecret));
        }

        _signingSecret = signingSecret;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public bool IsAuthenticated(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.AuthToken))
        {
            return false;
        }

        var separatorIndex = message.AuthToken.LastIndexOf(TokenSeparator);

        if (separatorIndex < 1 || separatorIndex == message.AuthToken.Length - 1)
        {
            return false;
        }

        var timestampPart = message.AuthToken[..separatorIndex];
        var signaturePart = message.AuthToken[(separatorIndex + 1)..];

        return long.TryParse(timestampPart, CultureInfo.InvariantCulture, out var messageTimestampUnixSeconds)
            && IsTimestampWithinWindow(messageTimestampUnixSeconds)
            && IsSignatureValid(timestampPart, signaturePart, message);
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
        var timestampAgeSeconds = currentUnixSeconds - messageTimestampUnixSeconds;

        return Math.Abs(timestampAgeSeconds) <= SignatureTimestampWindowSeconds;
    }

    private bool IsSignatureValid(string timestampPart, string signaturePart, InboundMessage message)
    {
        return signaturePart.Length > 0
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(ComputeSignature(timestampPart, message)),
                Encoding.UTF8.GetBytes(signaturePart));
    }

    private string ComputeSignature(string timestampPart, InboundMessage message)
    {
        var canonicalString = string.Concat(
            timestampPart, CanonicalFieldSeparator,
            message.RoutingKey.ToString(CultureInfo.InvariantCulture), CanonicalFieldSeparator,
            message.GameId, CanonicalFieldSeparator,
            message.CommandName, CanonicalFieldSeparator,
            message.CommandArguments);

        var signatureBytes = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret))
            .ComputeHash(Encoding.UTF8.GetBytes(canonicalString));

        return Convert.ToHexString(signatureBytes);
    }
}
