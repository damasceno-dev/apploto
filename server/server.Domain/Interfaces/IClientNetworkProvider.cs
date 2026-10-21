namespace server.Domain.Interfaces;

/// <summary>
/// The bounded network partition the current request came from, resolved by the host inside its configured proxy
/// trust boundary. Never a raw header value.
/// </summary>
public interface IClientNetworkProvider
{
    string Current();
}
