namespace GameServer.Messaging;

public sealed record InboundMessage(
    int RoutingKey,
    string GameId,
    string AuthToken,
    string CommandName,
    string CommandArguments)
{
    public static readonly InboundMessage NoMessage = new(0, string.Empty, string.Empty, string.Empty, string.Empty);
}
