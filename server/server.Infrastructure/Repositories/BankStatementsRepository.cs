using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class BankStatementsRepository(ServerDbContext db) : IBankStatementsRepository
{
    public async Task LockAccountBindingIdentity(Guid branchId, string bankIdentity, string accountIdentity, CancellationToken ct)
    {
        var identity = "loto:ofx-account-source:" + JsonSerializer.Serialize(new[] { branchId.ToString("N"), bankIdentity, accountIdentity });
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    public Task<bool> AccountBindingIdentityExists(Guid branchId, string bankIdentity, string accountIdentity, CancellationToken ct) =>
        db.BankStatementAccountBindings.AnyAsync(b => b.BranchId == branchId && b.BankIdentity == bankIdentity && b.AccountIdentity == accountIdentity, ct);
    public async Task AddAccountBinding(BankStatementAccountBinding binding, CancellationToken ct) => await db.BankStatementAccountBindings.AddAsync(binding, ct);
    public Task<BankStatementAccountBinding?> GetAccountBindingAsNoTracking(Guid id, Guid branchId, CancellationToken ct) => db.BankStatementAccountBindings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.BranchId == branchId && b.Active, ct);
    public async Task<IReadOnlyList<BankStatementAccountBinding>> ListAccountBindingsAsNoTracking(Guid branchId, Guid bankAccountId, CancellationToken ct) => await db.BankStatementAccountBindings.AsNoTracking().Where(b => b.BranchId == branchId && b.BankAccountId == bankAccountId && b.Active).OrderBy(b => b.Id).ToListAsync(ct);
    public Task<BankStatementImport?> FindImportAsNoTracking(Guid branchId, Guid bankAccountId, string fingerprint, CancellationToken ct) =>
        db.BankStatementImports.AsNoTracking().Include(i => i.Lines).ThenInclude(i => i.Line).SingleOrDefaultAsync(i => i.BranchId == branchId && i.BankAccountId == bankAccountId && i.Fingerprint == fingerprint, ct);
    public Task<BankStatementLine?> FindIdentityAsNoTracking(Guid branchId, Guid bankAccountId, string identity, CancellationToken ct) =>
        db.BankStatementLines.AsNoTracking().SingleOrDefaultAsync(i => i.BranchId == branchId && i.BankAccountId == bankAccountId && i.BankIdentity == identity, ct);
    public async Task AddImport(BankStatementImport import, CancellationToken ct) => await db.BankStatementImports.AddAsync(import, ct);
    public async Task AddLine(BankStatementLine line, CancellationToken ct) => await db.BankStatementLines.AddAsync(line, ct);
    public Task<BankStatementImport?> GetImportAsNoTracking(Guid id, Guid branchId, CancellationToken ct) =>
        db.BankStatementImports.AsNoTracking().Include(i => i.Lines).ThenInclude(i => i.Line).SingleOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct);
    public async Task<IReadOnlyList<BankReceipt>> PendingReceiptsAsNoTracking(Guid branchId, Guid bankAccountId, CancellationToken ct) =>
        await db.BankReceipts.AsNoTracking().Where(r => r.BranchId == branchId && r.BankAccountId == bankAccountId && r.Status == BankReceiptStatus.Pending).OrderBy(r => r.Id).Take(1001).ToListAsync(ct);
    public async Task AddProposal(BankMatchProposal proposal, CancellationToken ct) => await db.BankMatchProposals.AddAsync(proposal, ct);
    public Task<BankMatchProposal?> GetProposalAsNoTracking(Guid id, Guid branchId, CancellationToken ct) =>
        db.BankMatchProposals.AsNoTracking().Include(p => p.Line).SingleOrDefaultAsync(p => p.Id == id && p.BranchId == branchId, ct);
    public Task<bool> LineMatched(Guid lineId, CancellationToken ct) => db.BankStatementMatches.AnyAsync(m => m.LineId == lineId, ct);
    public Task<bool> ReceiptMatched(Guid receiptId, CancellationToken ct) => db.BankStatementMatches.AnyAsync(m => m.ReceiptId == receiptId, ct);
    public Task<BankReceipt?> GetReceipt(Guid id, Guid branchId, CancellationToken ct) => db.BankReceipts.SingleOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct);
    public async Task AddMatch(BankStatementMatch match, CancellationToken ct) => await db.BankStatementMatches.AddAsync(match, ct);
    public async Task<bool> ReceiptRequiresReview(Guid receiptId, CancellationToken ct)
    {
        var batch = await db.CardSettlementBatches.AsNoTracking().SingleOrDefaultAsync(b => b.BankReceiptId == receiptId, ct);
        if (batch is null) return false;
        var latest = await db.CardSettlementFeeEvidence.AsNoTracking().Where(e => e.BatchId == batch.Id).OrderByDescending(e => e.Sequence).FirstOrDefaultAsync(ct);
        var fee = latest?.ConfirmedFee ?? batch.ConfirmedFee;
        var observed = latest?.ObservedNetAmount ?? batch.ObservedNetAmount;
        return fee is null || observed is not null && observed != batch.GrossAmount - fee;
    }
}
