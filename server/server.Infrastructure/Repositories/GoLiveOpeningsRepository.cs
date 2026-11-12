using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class GoLiveOpeningsRepository(ServerDbContext db) : IGoLiveOpeningsRepository
{
    public Task<Guid?> GetSourceImportId(Guid branchId, Guid openingId, CancellationToken ct) =>
        db.GoLiveImportTargets.AsNoTracking().Where(x => x.BranchId == branchId)
            .Join(db.Transactions.AsNoTracking().Where(x => x.BranchId == branchId && x.GoLiveOpeningId == openingId),
                x => x.TargetId, t => t.Id, (x, t) => (Guid?)x.GoLiveImportId).FirstOrDefaultAsync(ct);

    public Task<bool> HasImportedDebt(Guid branchId, Guid accountId, Guid clientId, CancellationToken ct) =>
        db.GoLiveImportTargets.AsNoTracking().Where(x => x.BranchId == branchId && x.TargetKind == "ImportedDebt")
            .Join(db.Transactions.AsNoTracking().Where(x => x.BranchId == branchId && x.AccountId == accountId && x.ClientId == clientId && x.Active && x.Status == TransactionStatus.Active), x => x.TargetId, t => t.Id, (x, t) => t.Id).AnyAsync(ct);
    public async Task<IReadOnlyList<GoLiveOpening>> List(Guid branchId, CancellationToken ct) => await db.GoLiveOpenings.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
    public async Task Add(GoLiveOpening opening, CancellationToken ct) => await db.GoLiveOpenings.AddAsync(opening, ct);
    public Task<GoLiveOpening?> GetForUpdate(Guid branchId, Guid id, CancellationToken ct) => db.GoLiveOpenings.SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, ct);
    public Task<GoLiveOpening?> Get(Guid branchId, Guid id, CancellationToken ct) =>
        db.GoLiveOpenings.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, ct);
    public Task<bool> Exists(Guid branchId, string identity, CancellationToken ct) =>
        db.GoLiveOpenings.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.Identity == identity && x.Active, ct);
    public async Task<IReadOnlyList<Transaction>> Rows(Guid branchId, Guid id, CancellationToken ct) =>
        await db.Transactions.Where(x => x.BranchId == branchId && x.GoLiveOpeningId == id).OrderBy(x => x.DueDate).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<Guid?> GetClientCredit(Guid branchId, Guid clientId, CancellationToken ct) =>
        db.Transactions.AsNoTracking().Where(x => x.BranchId == branchId && x.ClientId == clientId && x.Active
            && x.Status != TransactionStatus.Draft && x.Direction == Direction.In && x.Account.Type == AccountType.Tab)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    public Task<bool> HasClientCredit(Guid branchId, Guid clientId, CancellationToken ct) =>
        db.Transactions.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.ClientId == clientId && x.Active
            && x.Status != TransactionStatus.Draft && x.Direction == Direction.In && x.Account.Type == AccountType.Tab, ct);
}
