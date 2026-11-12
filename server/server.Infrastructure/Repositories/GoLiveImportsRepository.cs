using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class GoLiveImportsRepository(ServerDbContext db) : IGoLiveImportsRepository
{
    public async Task<IReadOnlyList<GoLiveImportTarget>> Targets(Guid branchId, Guid importId, CancellationToken ct)
    {
        var saved = await db.GoLiveImportTargets.AsNoTracking().Where(x => x.BranchId == branchId && x.GoLiveImportId == importId).ToListAsync(ct);
        return saved.Concat(db.GoLiveImportTargets.Local.Where(x => x.BranchId == branchId && x.GoLiveImportId == importId && db.Entry(x).State == EntityState.Added)).DistinctBy(x => x.Id).OrderBy(x => x.SourceTable).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToList();
    }
    public async Task LockTargets(Guid branchId, CancellationToken ct)
    {
        // Stable selected configuration through the reviewed activation. Financial writers also
        // share the branch month/people coordination acquired before these row locks.
        foreach (var table in new[] { "Accounts", "Categories", "Clients", "Operators", "Products", "Settings", "Terminals", "EmployeePayCategories", "TimeEntryPolicies" })
            await db.Database.ExecuteSqlRawAsync($$"""SELECT "Id" FROM "{{table}}" WHERE "BranchId" = {0} ORDER BY "Id" FOR SHARE""", [branchId], ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT t."Id" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id"=t."CategoryId" WHERE c."BranchId"={branchId} ORDER BY t."Id" FOR SHARE OF t""", ct);
    }
    public Task<DateTime?> ActivatedLockDate(Guid branchId, CancellationToken ct) => db.GoLiveImports.AsNoTracking().Where(x => x.BranchId == branchId && x.Status == "Activated").Select(x => x.SourceLockDate).SingleOrDefaultAsync(ct);
    public async Task Audit(GoLiveImportAudit audit, CancellationToken ct) => await db.GoLiveImportAudits.AddAsync(audit, ct);
    public async Task Register(GoLiveImportTarget target, CancellationToken ct) => await db.GoLiveImportTargets.AddAsync(target, ct);
    public Task<bool> HasActivated(Guid branchId, CancellationToken ct) => db.GoLiveImports.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.Status == "Activated", ct);
    public async Task Add(GoLiveImport import, CancellationToken ct) => await db.GoLiveImports.AddAsync(import, ct);
    public Task<GoLiveImport?> GetForUpdate(Guid branchId, Guid id, CancellationToken ct) => db.GoLiveImports.SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, ct);
    public Task<GoLiveImport?> Get(Guid branchId, Guid id, CancellationToken ct) => db.GoLiveImports.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, ct);
    public Task<GoLiveImport?> FindDigest(Guid branchId, string digest, CancellationToken ct) => db.GoLiveImports.SingleOrDefaultAsync(x => x.BranchId == branchId && x.SourceDigest == digest, ct);
    public async Task<IReadOnlyList<GoLiveImport>> List(Guid branchId, CancellationToken ct) => await db.GoLiveImports.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(ct);
    public async Task<string> Fingerprint(Guid branchId, CancellationToken ct)
    {
        // Include every operational row and xmin; no customer content leaves this repository.
        // Import staging and idempotency reservations are deliberately outside operational state.
        var parts = new List<string>();
        foreach (var table in new[] { "Accounts", "Categories", "Clients", "Operators", "DailyCloses", "Transactions", "Products", "Settings", "GoLiveOpenings", "TimeEntries", "HourBankEntries", "EmployeePayItems", "EmployeePayCategories", "TimeEntryPolicies", "SpecialDraws", "Terminals", "OpeningCounts", "SettlementEvents", "BankReceipts", "SessionFieldEffects", "EmployeeCommissionRules", "EmployeeCommissionSources", "EmployeeCommissionFeePostings", "SpecialDrawAllocations", "SessionEvents", "OpeningDiscrepancyAdjustments" })
        {
            var sql = $$"""SELECT COALESCE(string_agg("Id"::text || ':' || xmin::text, ',' ORDER BY "Id"), '') AS "Value" FROM "{{table}}" WHERE "BranchId" = {0}""";
            parts.Add(table + ":" + await db.Database.SqlQueryRaw<string>(sql, branchId).SingleAsync(ct));
        }
        var types = await db.Database.SqlQuery<string>($"""SELECT COALESCE(string_agg(t."Id"::text || ':' || t.xmin::text, ',' ORDER BY t."Id"), '') AS "Value" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId" WHERE c."BranchId" = {branchId}""").SingleAsync(ct);
        parts.Add(types);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts))));
    }
}
