using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class GoLiveHistoryRepository(ServerDbContext db) : IGoLiveHistoryRepository
{
    public async Task Add<T>(T row, CancellationToken ct) where T : EntityBase => await db.Set<T>().AddAsync(row, ct);
    public async Task<bool> HasCashboxHistory(Guid branchId, Guid accountId, CancellationToken ct) =>
        await db.DailyCloses.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.AccountId == accountId, ct)
        || await db.OpeningCounts.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.AccountId == accountId, ct);
}
