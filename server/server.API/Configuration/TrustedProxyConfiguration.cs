using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace server.Configuration;

/// <summary>
/// The only way a forwarded client address is believed: <c>ForwardedHeaders:KnownProxies</c> (addresses) and
/// <c>ForwardedHeaders:KnownNetworks</c> (CIDR). With neither configured the forwarded-headers middleware does nothing
/// and the socket peer is the client. With them, the framework unwraps <c>X-Forwarded-For</c> one hop at a time and
/// stops at the first peer that is not trusted, so a header sent by anyone else is ignored.
/// </summary>
internal static class TrustedProxyConfiguration
{
    public static void AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        Read(configuration); // An invalid entry stops startup instead of silently trusting nothing or everything.
        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, Setup>();
    }

    internal static (IReadOnlyList<IPAddress> Proxies, IReadOnlyList<System.Net.IPNetwork> Networks) Read(IConfiguration configuration)
    {
        var proxies = (configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            .Select(value => IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException("ForwardedHeaders:KnownProxies contém um endereço IP inválido."))
            .ToList();
        var networks = (configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            .Select(value => System.Net.IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException("ForwardedHeaders:KnownNetworks contém uma rede CIDR inválida."))
            .ToList();
        return (proxies, networks);
    }

    private sealed class Setup(IConfiguration configuration) : IConfigureOptions<ForwardedHeadersOptions>
    {
        public void Configure(ForwardedHeadersOptions options)
        {
            var (proxies, networks) = Read(configuration);
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            if (proxies.Count == 0 && networks.Count == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = null;
            foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
            foreach (var network in networks) options.KnownIPNetworks.Add(network);
        }
    }
}
