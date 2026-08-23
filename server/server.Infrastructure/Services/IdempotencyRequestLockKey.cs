using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace server.Infrastructure.Services;

public static class IdempotencyRequestLockKey
{
    public static long Compute(string endpoint, Guid branchId, Guid userId, string key)
    {
        var source = $"idempotency:{endpoint}:{branchId:N}:{userId:N}:{key}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long)));
    }
}
