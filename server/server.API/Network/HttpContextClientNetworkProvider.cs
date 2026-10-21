using server.Domain.Interfaces;

namespace server.Network;

public sealed class HttpContextClientNetworkProvider(IHttpContextAccessor accessor) : IClientNetworkProvider
{
    public string Current() => ClientNetwork.Of(accessor.HttpContext?.Connection.RemoteIpAddress);
}
