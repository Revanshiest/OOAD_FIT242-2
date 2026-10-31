using System.Collections.Frozen;

namespace GameServer.Endpoint;

using GameServer.Messaging;

public interface IAuthenticator
{
    bool IsAuthenticated(InboundMessage message);
}

public sealed class WhitelistTokenAuthenticator : IAuthenticator
{
    private readonly FrozenSet<string> _allowedTokens;

    public WhitelistTokenAuthenticator(IEnumerable<string> allowedTokens)
    {
        ArgumentNullException.ThrowIfNull(allowedTokens);

        _allowedTokens = allowedTokens.ToFrozenSet(StringComparer.Ordinal);
    }

    public bool IsAuthenticated(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return !string.IsNullOrWhiteSpace(message.AuthToken) && _allowedTokens.Contains(message.AuthToken);
    }
}
