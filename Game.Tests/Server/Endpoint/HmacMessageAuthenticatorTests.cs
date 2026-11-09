using GameServer.Endpoint;
using GameServer.Messaging;
using Game.Tests.Server;

namespace Game.Tests.Server.Endpoints;

public class HmacMessageAuthenticatorTests
{
    private const string SigningSecret = "test-signing-secret";
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _timeProvider = new(TestTimestamp);

    private InboundMessage CreateMessage(string authToken = "placeholder")
    {
        return new InboundMessage(3, "game-1", authToken, "Move", """{"itemId":"ship1"}""");
    }

    [Fact]
    public void IsAuthenticated_AcceptsMessageWithValidSignature()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var message = CreateMessage(authenticator.Sign(CreateMessage()));

        Assert.True(authenticator.IsAuthenticated(message));
    }

    [Fact]
    public void IsAuthenticated_RejectsMessageWithTamperedCommandArguments()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var signedMessage = CreateMessage(authenticator.Sign(CreateMessage()));
        var tamperedMessage = signedMessage with { CommandArguments = """{"itemId":"hacked"}""" };

        Assert.False(authenticator.IsAuthenticated(tamperedMessage));
    }

    [Fact]
    public void IsAuthenticated_RejectsMessageSignedWithDifferentSecret()
    {
        var signingAuthenticator = new HmacMessageAuthenticator("secret-a", _timeProvider);
        var verifyingAuthenticator = new HmacMessageAuthenticator("secret-b", _timeProvider);
        var message = CreateMessage(signingAuthenticator.Sign(CreateMessage()));

        Assert.False(verifyingAuthenticator.IsAuthenticated(message));
    }

    [Fact]
    public void IsAuthenticated_RejectsAlteredRoutingKey_WithValidSignatureForOriginalKey()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var signedMessage = CreateMessage(authenticator.Sign(CreateMessage()));
        var reroutedMessage = signedMessage with { RoutingKey = 4 };

        Assert.False(authenticator.IsAuthenticated(reroutedMessage));
    }

    [Theory]
    [InlineData("not-a-signed-token")]
    [InlineData(".deadbeef")]
    [InlineData("not-a-number.")]
    [InlineData("")]
    [InlineData(" ")]
    public void IsAuthenticated_RejectsMalformedTokens(string authToken)
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);

        Assert.False(authenticator.IsAuthenticated(CreateMessage(authToken)));
    }

    [Fact]
    public void IsAuthenticated_AcceptsTimestampAtExactWindowBoundary()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var boundaryMessage = CreateMessage(SignAt(
            TestTimestamp.AddSeconds(-HmacMessageAuthenticator.SignatureTimestampWindowSeconds)));

        Assert.True(authenticator.IsAuthenticated(boundaryMessage));
    }

    [Fact]
    public void IsAuthenticated_RejectsTimestampOneSecondBeyondWindow()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var expiredMessage = CreateMessage(SignAt(
            TestTimestamp.AddSeconds(-HmacMessageAuthenticator.SignatureTimestampWindowSeconds - 1)));

        Assert.False(authenticator.IsAuthenticated(expiredMessage));
    }

    [Fact]
    public void IsAuthenticated_AcceptsFutureTimestampWithinClockSkewWindow()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var skewedMessage = CreateMessage(SignAt(
            TestTimestamp.AddSeconds(HmacMessageAuthenticator.SignatureTimestampWindowSeconds)));

        Assert.True(authenticator.IsAuthenticated(skewedMessage));
    }

    [Fact]
    public void IsAuthenticated_RejectsMessageAfterClockAdvancesBeyondWindow()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var message = CreateMessage(authenticator.Sign(CreateMessage()));

        _timeProvider.Advance(TimeSpan.FromSeconds(HmacMessageAuthenticator.SignatureTimestampWindowSeconds + 1));

        Assert.False(authenticator.IsAuthenticated(message));
    }

    [Fact]
    public void HmacMessageAuthenticator_SignsAndAuthenticatesMessagesCorrectly()
    {
        var authenticator = new HmacMessageAuthenticator("super-secret-key-12345", _timeProvider);

        var message = new InboundMessage(0, "game-1", string.Empty, "Move", "{}");
        var signedToken = authenticator.Sign(message);

        Assert.False(string.IsNullOrWhiteSpace(signedToken));

        var signedMessage = message with { AuthToken = signedToken };
        Assert.True(authenticator.IsAuthenticated(signedMessage));

        var tamperedMessage = signedMessage with { AuthToken = signedToken + "tampered" };
        Assert.False(authenticator.IsAuthenticated(tamperedMessage));
    }

    [Fact]
    public void IsAuthenticated_BlocksReplayAttacks_WithExactSameToken()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var message = CreateMessage();
        var token = authenticator.Sign(message);
        var signedMessage = message with { AuthToken = token };

        Assert.True(authenticator.IsAuthenticated(signedMessage));
        Assert.False(authenticator.IsAuthenticated(signedMessage));
    }

    [Fact]
    public void IsAuthenticated_RejectsSignatureReusedWithDifferentMessageId()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var original = CreateMessage() with { MessageId = "first" };
        var signed = original with { AuthToken = authenticator.Sign(original) };

        Assert.True(authenticator.IsAuthenticated(signed));
        Assert.False(authenticator.IsAuthenticated(signed with { MessageId = "second" }));
    }

    [Fact]
    public void IsAuthenticated_RejectsTimestampThatWouldOverflowAgeCalculation()
    {
        var authenticator = new HmacMessageAuthenticator(SigningSecret, _timeProvider);
        var timestamp = unchecked(TestTimestamp.ToUnixTimeSeconds() - long.MinValue);

        Assert.False(authenticator.IsAuthenticated(CreateMessage($"{timestamp}.00")));
        Assert.False(authenticator.IsAuthenticated(CreateMessage($"{long.MinValue}.00")));
        Assert.False(authenticator.IsAuthenticated(CreateMessage($"{long.MaxValue}.00")));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenSecretIsMissing()
    {
        Assert.Throws<ArgumentException>(() => new HmacMessageAuthenticator("  ", _timeProvider));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTimeProviderIsMissing()
    {
        Assert.Throws<ArgumentNullException>(() => new HmacMessageAuthenticator(SigningSecret, null!));
    }

    private string SignAt(DateTimeOffset signingTimestamp)
    {
        var signingAuthenticator = new HmacMessageAuthenticator(SigningSecret, new FixedTimeProvider(signingTimestamp));

        return signingAuthenticator.Sign(CreateMessage());
    }
}
