using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace server.Infrastructure.Services;

/// <summary>
/// Stable branch-level advisory key for Terminal↔Tab pairing changes and account deactivation.
/// The namespace prefix keeps it apart from the month-lock and account-history keys; no pairing
/// write holds either of those, so the keys never form a lock-order cycle.
/// </summary>
public static class AccountPairingCoordinationKey
{
    public static long Compute(Guid branchId)
    {
        var source = $"account-pairing:{branchId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long)));
    }
}
