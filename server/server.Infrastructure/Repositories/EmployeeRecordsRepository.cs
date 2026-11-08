using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class EmployeeRecordsRepository(ServerDbContext db) : IEmployeeRecordsRepository
{
    // Runs after the shared month boundary in the same transaction; serializes rule windows, revision heads,
    // allocations and opening balances within a branch. No dependency on another lane's services.
    public async Task LockBranch(Guid branchId, CancellationToken ct = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"people:" + branchId.ToString()}, 0))", ct);
    public async Task Add<T>(T record, CancellationToken ct = default) where T : EntityBase => await db.Set<T>().AddAsync(record, ct);
    public async Task<IReadOnlyList<T>> List<T>(Guid branchId, CancellationToken ct = default) where T : EntityBase =>
        await db.Set<T>().AsNoTracking().Where(r => EF.Property<Guid>(r, "BranchId") == branchId).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(ct);
}
