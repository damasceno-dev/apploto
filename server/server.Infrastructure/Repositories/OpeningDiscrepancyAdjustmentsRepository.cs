using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class OpeningDiscrepancyAdjustmentsRepository(ServerDbContext db) : IOpeningDiscrepancyAdjustmentsRepository
{
    public async Task<SessionOpeningDiscrepancy?> LockSource(Guid branchId, Guid discrepancyId, CancellationToken ct)
    {
        var source = await db.SessionOpeningDiscrepancies.FromSqlInterpolated(
            $"SELECT *, xmin FROM \"SessionOpeningDiscrepancies\" WHERE \"Id\" = {discrepancyId} AND \"BranchId\" = {branchId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (source is not null) await db.Entry(source).ReloadAsync(ct);
        return source;
    }

    public Task<OpeningDiscrepancyAdjustment?> Latest(Guid branchId, Guid discrepancyId, CancellationToken ct) =>
        db.OpeningDiscrepancyAdjustments.AsNoTracking().Where(a => a.BranchId == branchId && a.DiscrepancyId == discrepancyId)
            .OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OpeningDiscrepancyAdjustment>> List(Guid branchId, Guid discrepancyId, int afterSequence, int take, CancellationToken ct) =>
        await db.OpeningDiscrepancyAdjustments.AsNoTracking()
            .Where(a => a.BranchId == branchId && a.DiscrepancyId == discrepancyId && a.Sequence > afterSequence)
            .OrderBy(a => a.Sequence).Take(take).ToListAsync(ct);

    public async Task Add(OpeningDiscrepancyAdjustment adjustment, CancellationToken ct) =>
        await db.OpeningDiscrepancyAdjustments.AddAsync(adjustment, ct);
}
