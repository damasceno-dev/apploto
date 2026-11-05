using Microsoft.EntityFrameworkCore;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class BankingReadLocks(ServerDbContext db) : IBankingReadLocks
{
    public async Task Accounts(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "Id" FROM "Accounts" WHERE "BranchId" = {branchId} AND "Id" = ANY({ids.ToArray()}) ORDER BY "Id" FOR SHARE
            """, ct);
    public async Task Transactions(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "Id" FROM "Transactions" WHERE "BranchId" = {branchId} AND "Id" = ANY({ids.ToArray()}) ORDER BY "Id" FOR SHARE
            """, ct);
    public async Task TransactionTypes(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT t."Id" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = {branchId} AND t."Id" = ANY({ids.ToArray()}) ORDER BY t."Id" FOR SHARE
            """, ct);
}
