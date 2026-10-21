using System.Net;
using System.Net.Sockets;

namespace server.Network;

/// <summary>
/// The bounded partition abuse budgets use for a client address: an IPv4 address (IPv4-mapped IPv6 included), or the
/// /64 an IPv6 address belongs to, since one subscriber usually controls a whole /64. No address is one shared
/// <see cref="Unknown"/> partition. The address is the connection's, after the configured forwarded-header trust.
/// </summary>
internal static class ClientNetwork
{
    public const string Unknown = "unknown";

    public static string Of(IPAddress? address)
    {
        if (address is null) return Unknown;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork) return "v4:" + address;
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return Unknown;
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return "v6:" + new IPAddress(bytes) + "/64";
    }
}
