using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Repositories;

internal class SettlementEventsRepository(ServerDbContext dbContext) : ISettlementEventsRepository
{
    public async Task Add(SettlementEvent settlementEvent, CancellationToken ct = default)
    {
        await dbContext.SettlementEvents.AddAsync(settlementEvent, ct);
    }

    public async Task<IReadOnlyList<SettlementEvent>> ListByReceivableIdsAndBranchIdAsNoTracking(
        IReadOnlyCollection<Guid> receivableTransactionIds,
        Guid branchId,
        CancellationToken ct = default)
    {
        if (receivableTransactionIds.Count == 0)
            return [];

        return await dbContext.SettlementEvents
            .AsNoTracking()
            .Where(e => e.BranchId == branchId && receivableTransactionIds.Contains(e.ReceivableTransactionId))
            .OrderBy(e => e.ReceivableTransactionId)
            .ThenBy(e => e.Sequence)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SettlementEvent>> ListByBankReceiptIdAndBranchIdAsNoTracking(
        Guid bankReceiptId,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.SettlementEvents
            .AsNoTracking()
            .Where(e => e.BranchId == branchId && e.BankReceiptId == bankReceiptId)
            .OrderBy(e => e.ReceivableTransactionId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SettlementEvent>> ListReturnsOfDepositEventsAsNoTracking(
        IReadOnlyCollection<Guid> depositEventIds,
        Guid branchId,
        CancellationToken ct = default)
    {
        if (depositEventIds.Count == 0)
            return [];

        return await dbContext.SettlementEvents
            .AsNoTracking()
            .Where(e =>
                e.BranchId == branchId &&
                e.Kind == SettlementEventKind.ChequeReturned &&
                e.ReversesEventId != null &&
                depositEventIds.Contains(e.ReversesEventId.Value))
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByReceivableIdAsNoTracking(Guid receivableTransactionId, CancellationToken ct = default)
    {
        return dbContext.SettlementEvents
            .AsNoTracking()
            .AnyAsync(e => e.ReceivableTransactionId == receivableTransactionId, ct);
    }

    public Task<bool> ExistsCommandEventByReceivableIdAsNoTracking(Guid receivableTransactionId, CancellationToken ct = default)
    {
        return dbContext.SettlementEvents
            .AsNoTracking()
            .AnyAsync(e =>
                e.ReceivableTransactionId == receivableTransactionId &&
                e.Provenance == SettlementEventProvenance.Command,
                ct);
    }
}

internal class BankReceiptsRepository(ServerDbContext dbContext) : IBankReceiptsRepository
{
    public Task<BankMovement?> GetMovementAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default) =>
        dbContext.BankMovements.AsNoTracking().Include(m => m.SourceTransaction).SingleOrDefaultAsync(m => m.BankReceiptId == receiptId && m.BranchId == branchId, ct);
    public Task<BankReceiptReturnSource?> GetReturnSourceAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default) =>
        (from receipt in dbContext.BankReceipts.AsNoTracking()
         join returned in dbContext.SettlementEvents.AsNoTracking() on receipt.SourceSettlementEventId equals (Guid?)returned.Id
         join deposit in dbContext.SettlementEvents.AsNoTracking() on returned.ReversesEventId equals (Guid?)deposit.Id
         where receipt.Id == receiptId && receipt.BranchId == branchId
         select new BankReceiptReturnSource(returned.ReceivableTransactionId, deposit.BankReceiptId)).SingleOrDefaultAsync(ct);
    public Task<CardSettlementBatch?> GetCardBatchAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default) =>
        dbContext.CardSettlementBatches.AsNoTracking().SingleOrDefaultAsync(b => b.BankReceiptId == receiptId && b.BranchId == branchId, ct);
    public async Task<IReadOnlyList<CardSettlementFeeEvidence>> ListFeeEvidenceAsNoTracking(Guid batchId, Guid branchId, CancellationToken ct = default) =>
        await dbContext.CardSettlementFeeEvidence.AsNoTracking().Where(e => e.BatchId == batchId && e.BranchId == branchId).OrderBy(e => e.Sequence).ToListAsync(ct);
    public async Task Add(BankReceipt receipt, CancellationToken ct = default)
    {
        await dbContext.BankReceipts.AddAsync(receipt, ct);
    }

    public Task<BankReceipt?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return dbContext.BankReceipts
            .AsNoTracking()
            .Include(receipt => receipt.BankAccount)
            .SingleOrDefaultAsync(receipt => receipt.Id == id && receipt.BranchId == branchId, ct);
    }
}

internal class TransactionClassificationRepository(ServerDbContext dbContext) : ITransactionClassificationRepository
{
    public async Task<IReadOnlyList<Transaction>> ListReclassificationCandidatesAsNoTracking(
        Guid transactionTypeId,
        Guid branchId,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.GenerationKind != TransactionGenerationKind.BankMovement &&
                t.TransactionTypeId == transactionTypeId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.Date >= dateFrom &&
                t.Date <= dateTo &&
                (t.PostedSettlementInstrument != t.TransactionType.SettlementInstrument ||
                 t.PostedMonthlyFinancialRole != t.TransactionType.MonthlyFinancialRole ||
                 t.PostedFinancialClass != t.TransactionType.FinancialClass) &&
                // A receivable with a settlement lifecycle keeps the instrument its events were recorded under, so
                // reclassification never strands a deposit, return or pending receipt (§6.24).
                (t.PostedSettlementInstrument == t.TransactionType.SettlementInstrument ||
                 dbContext.SettlementEvents.Any(e =>
                     e.ReceivableTransactionId == t.Id && e.Provenance == SettlementEventProvenance.Command) == false))
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> ListReclassifiedRowsAsNoTracking(
        IReadOnlyCollection<Guid> transactionIds,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.BranchId == branchId && transactionIds.Contains(t.Id))
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task AddRevisions(IReadOnlyCollection<TransactionClassificationRevision> revisions, CancellationToken ct = default)
    {
        await dbContext.TransactionClassificationRevisions.AddRangeAsync(revisions, ct);
    }

    public async Task<bool> TryApplyRevision(
        TransactionClassificationRevision revision,
        uint expectedVersion,
        DateTime classifiedAt,
        CancellationToken ct = default)
    {
        // The row's xmin is its version; TR_Transactions_Classification accepts the change only together with the
        // saved revision of the next number.
        var updated = await dbContext.Transactions
            .Where(t =>
                t.Id == revision.TransactionId &&
                t.BranchId == revision.BranchId &&
                t.Version == expectedVersion &&
                t.ClassificationRevision == revision.Revision - 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.PostedSettlementInstrument, revision.SettlementInstrument)
                .SetProperty(t => t.PostedMonthlyFinancialRole, revision.MonthlyFinancialRole)
                .SetProperty(t => t.PostedFinancialClass, revision.FinancialClass)
                .SetProperty(t => t.ClassificationProvenance, ClassificationProvenance.OpenPeriodReclassification)
                .SetProperty(t => t.ClassificationRevision, revision.Revision)
                .SetProperty(t => t.ClassifiedAt, classifiedAt),
                ct);
        return updated == 1;
    }
}
