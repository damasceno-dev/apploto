using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class SecurityCoordination(ServerDbContext dbContext) : ISecurityCoordination
{
    internal static long Key(string resource) => BinaryPrimitives.ReadInt64BigEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes("loto:m8:" + resource)));

    public async Task<ISecurityScope?> TryAcquire(params string[] resources)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT set_config('lock_timeout', '5000ms', true)");
            foreach (var key in resources.Select(Key).Distinct().Order())
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
            return new Scope(transaction);
        }
        catch (PostgresException exception) when (exception.SqlState == "55P03")
        {
            await transaction.DisposeAsync();
            return null;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task HoldSharedInCurrentTransaction(string resource, CancellationToken ct = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A shared security lock needs the caller's open transaction.");
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({Key(resource)})", ct);
    }

    private sealed class Scope(IDbContextTransaction transaction) : ISecurityScope
    {
        public Task Complete() => transaction.CommitAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
