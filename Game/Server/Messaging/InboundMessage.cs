namespace GameServer.Messaging;

public sealed record InboundMessage(
    int RoutingKey,
    string GameId,
    string AuthToken,
    string CommandName,
    string CommandArguments,
    string? MessageId = null)
{
    public static readonly InboundMessage NoMessage = new(-1, "__none__", string.Empty, "None", "{}");
}
