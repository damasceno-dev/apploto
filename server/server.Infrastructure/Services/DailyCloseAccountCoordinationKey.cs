using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace server.Infrastructure.Services;

/// <summary>
/// Stable PostgreSQL advisory-lock key shared by production coordination and integration tests.
/// SHA-256's first eight bytes are interpreted as a signed big-endian 64-bit integer.
/// </summary>
public static class DailyCloseAccountCoordinationKey
{
    public static long Compute(Guid branchId, Guid accountId)
    {
        var source = $"{branchId:N}:{accountId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long)));
    }
}
