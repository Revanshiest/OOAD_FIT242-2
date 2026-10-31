using System.Collections.Frozen;

namespace GameServer.Endpoint;

public interface IAuthenticator
{
    bool IsAuthenticated(string authToken);
}

public sealed class WhitelistTokenAuthenticator : IAuthenticator
{
    private readonly FrozenSet<string> _allowedTokens;

    public WhitelistTokenAuthenticator(IEnumerable<string> allowedTokens)
    {
        ArgumentNullException.ThrowIfNull(allowedTokens);

        _allowedTokens = allowedTokens.ToFrozenSet(StringComparer.Ordinal);
    }

    public bool IsAuthenticated(string authToken)
    {
        return !string.IsNullOrWhiteSpace(authToken) && _allowedTokens.Contains(authToken);
    }
}
