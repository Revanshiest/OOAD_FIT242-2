using System.Text.Json;
using GameServer.Endpoint;
using GameServer.Messaging;

namespace Game.Tests.Server.Endpoints;

public class InboundMessageParserTests
{
    private readonly InboundMessageParser _messageParser = new();

    [Fact]
    public void Parse_ExtractsAllRoutingFields_AndKeepsRawCommandArguments()
    {
        const string requestPayload = """
            {"key": 3, "id": "game-1", "token": "secret", "command": "Move", "args": {"itemId": "ship1"}}
            """;

        var inboundMessage = _messageParser.Parse(requestPayload);

        Assert.Equal(new InboundMessage(3, "game-1", "secret", "Move", """{"itemId": "ship1"}"""),
            inboundMessage with { CommandArguments = inboundMessage.CommandArguments });
        Assert.Equal(3, inboundMessage.RoutingKey);
        Assert.Equal("game-1", inboundMessage.GameId);
        Assert.Equal("secret", inboundMessage.AuthToken);
        Assert.Equal("Move", inboundMessage.CommandName);
        Assert.Contains("ship1", inboundMessage.CommandArguments);
    }

    [Fact]
    public void Parse_SubstitutesEmptyArguments_WhenArgsFieldIsAbsent()
    {
        const string requestPayload = """{"key": 0, "id": "game-1", "token": "secret", "command": "Move"}""";

        var inboundMessage = _messageParser.Parse(requestPayload);

        Assert.Equal("{}", inboundMessage.CommandArguments);
    }

    [Fact]
    public void Parse_ParsesMessageId_WhenPresent()
    {
        const string payload = """{"key":0,"id":"game-1","token":"secret","command":"Move","messageId":"msg-12345"}""";

        var message = _messageParser.Parse(payload);

        Assert.Equal("msg-12345", message.MessageId);
    }

    [Fact]
    public void Parse_PreservesNestedArgumentStructure()
    {
        const string requestPayload = """
            {"key": 0, "id": "game-1", "token": "secret", "command": "CreateItem", "args": {"position": [1, 2], "speed": [3, 4]}}
            """;

        var inboundMessage = _messageParser.Parse(requestPayload);

        Assert.Contains("position", inboundMessage.CommandArguments);
        Assert.Contains("speed", inboundMessage.CommandArguments);
    }

    [Theory]
    [InlineData("""{"id": "game-1", "token": "secret", "command": "Move"}""", "key")]
    [InlineData("""{"key": 0, "token": "secret", "command": "Move"}""", "id")]
    [InlineData("""{"key": 0, "id": "game-1", "token": "secret"}""", "command")]
    public void Parse_ThrowsArgumentException_WhenRequiredFieldIsMissing(string requestPayload, string _)
    {
        Assert.Throws<ArgumentException>(() => _messageParser.Parse(requestPayload));
    }

    [Fact]
    public void Parse_AllowsMissingToken_AndDefaultsToEmptyString()
    {
        const string requestPayload = """{"key": 0, "id": "game-1", "command": "Move"}""";
        var message = _messageParser.Parse(requestPayload);
        Assert.Equal(string.Empty, message.AuthToken);
    }

    [Fact]
    public void Parse_ThrowsArgumentException_WhenRoutingKeyIsNotANumber()
    {
        const string requestPayload = """{"key": "zero", "id": "game-1", "token": "secret", "command": "Move"}""";

        Assert.Throws<ArgumentException>(() => _messageParser.Parse(requestPayload));
    }

    [Fact]
    public void Parse_ThrowsArgumentException_WhenPayloadIsNotAJsonObject()
    {
        const string requestPayload = "[1, 2, 3]";

        Assert.Throws<ArgumentException>(() => _messageParser.Parse(requestPayload));
    }

    [Fact]
    public void Parse_ThrowsJsonException_WhenPayloadIsMalformed()
    {
        const string requestPayload = "not-a-json";

        Assert.ThrowsAny<JsonException>(() => _messageParser.Parse(requestPayload));
    }

    [Fact]
    public void Parse_ThrowsArgumentNullException_WhenPayloadIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _messageParser.Parse(null!));
    }
}
