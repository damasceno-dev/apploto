using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace server.Infrastructure.Services;

/// <summary>
/// Stable branch-level advisory key for the month-lock boundary. The namespace prefix makes
/// a collision with an account-history key vanishingly unlikely; lock ordering still assumes
/// the two key namespaces do not collide.
/// </summary>
public static class MonthLockCoordinationKey
{
    public static long Compute(Guid branchId)
    {
        var source = $"month-lock:{branchId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long)));
    }
}
