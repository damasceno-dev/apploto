using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class ReportReadSnapshot(ServerDbContext dbContext) : IReportReadSnapshot
{
    public async Task<IAsyncDisposable> Begin(CancellationToken ct = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            // First statement of the transaction, before any query takes the snapshot.
            await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        return new Scope(transaction);
    }

    private sealed class Scope(IDbContextTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception exception) when (exception is InvalidOperationException or DbException)
            {
                // A broken or already-aborted connection ends the transaction itself; disposing below releases it,
                // and the caller's original failure is never masked by this cleanup.
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
