using Microsoft.EntityFrameworkCore;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class FinancialResultRepository(ServerDbContext db) : IFinancialResultRepository
{
    public async Task<FinancialResultPage> Read(
        Guid branchId, Guid varianceProductId, DateTime from, DateTime to,
        FinancialClass? financialClass, int page, int pageSize, CancellationToken ct = default)
    {
        // Generated counterparts describe the same fact. Session fee/report effects are primary sources;
        // for recolhimento the bank leg is primary. Settlement principal and receipt confirmation are not income.
        var transactions = db.Transactions.AsNoTracking()
            .Where(t => t.BranchId == branchId && t.Active && t.Status == TransactionStatus.Active &&
                t.GenerationKind != TransactionGenerationKind.FiadoSale &&
                t.GenerationKind != TransactionGenerationKind.FiadoCashRepayment &&
                t.GenerationKind != TransactionGenerationKind.SafeCashDeposit &&
                t.GenerationKind != TransactionGenerationKind.ChequeFiadoConversion &&
                !db.BankMovements.Any(m => m.BranchId == branchId && m.SourceTransactionId == t.Id))
            .Select(t => new FinancialResultEntry
            {
                AccountName = t.Account.Name, AccountActive = t.Account.Active, OfficialAttemptId = null,
                SourceHistoryComplete = t.ClassificationProvenance != ClassificationProvenance.MigratedFromTypeAtUpgrade,
                TransactionId = t.Id, BankReceiptId = db.BankMovements.Where(m => m.BankTransactionId == t.Id && m.BranchId == branchId)
                    .Select(m => (Guid?)m.BankReceiptId).FirstOrDefault(),
                SourceId = t.Id, SourceKind = FinancialResultSourceKind.Transaction, AccountId = t.AccountId, SessionId = t.SessionId,
                SourceDate = db.BankMovements.Where(m => m.BankTransactionId == t.Id && m.BranchId == branchId)
                    .Select(m => (DateTime?)m.BankReceipt.OccurredOn).FirstOrDefault() ?? t.Date, PostedOn = t.Date,
                SnapshotFinancialClass = t.PostedFinancialClass,
                ClassificationConflict = t.Account.Type == AccountType.Tab && t.PostedFinancialClass != null &&
                    t.PostedFinancialClass != FinancialClass.Unclassified && t.PostedFinancialClass != FinancialClass.CaixaAndClientMoney,
                FinancialClass = t.Account.Type == AccountType.Tab && t.PostedFinancialClass != null &&
                    t.PostedFinancialClass != FinancialClass.Unclassified && t.PostedFinancialClass != FinancialClass.CaixaAndClientMoney
                    ? FinancialClass.Unclassified : t.PostedFinancialClass ?? FinancialClass.Unclassified,
                Provenance = t.ClassificationProvenance,
                SignedAmount = t.Direction == Direction.In ? t.Value : -t.Value
            });
        var initialFees = db.CardSettlementBatches.AsNoTracking().Where(b => b.BranchId == branchId)
            .Select(b => new FinancialResultEntry
            {
                AccountName = b.BankReceipt.BankAccount.Name, AccountActive = b.BankReceipt.BankAccount.Active, OfficialAttemptId = null, SourceHistoryComplete = true,
                TransactionId = null, BankReceiptId = b.BankReceiptId,
                SourceId = b.Id, SourceKind = FinancialResultSourceKind.CardSettlementBatchFee, AccountId = b.BankReceipt.BankAccountId,
                SessionId = null, SourceDate = b.BankReceipt.OccurredOn, PostedOn = b.BankReceipt.PostedOn,
                SnapshotFinancialClass = b.FeeFinancialClass, ClassificationConflict = false, FinancialClass = b.FeeFinancialClass, Provenance = null, SignedAmount = -b.ConfirmedFee
            });
        var feeRevisions = db.CardSettlementFeeEvidence.AsNoTracking().Where(e => e.BranchId == branchId)
            .Select(e => new FinancialResultEntry
            {
                AccountName = e.Batch.BankReceipt.BankAccount.Name, AccountActive = e.Batch.BankReceipt.BankAccount.Active, OfficialAttemptId = null, SourceHistoryComplete = true,
                TransactionId = null, BankReceiptId = e.Batch.BankReceiptId,
                SourceId = e.Id, SourceKind = FinancialResultSourceKind.CardSettlementFeeEvidence, AccountId = e.Batch.BankReceipt.BankAccountId,
                SessionId = null, SourceDate = e.OccurredOn, PostedOn = e.PostedOn,
                SnapshotFinancialClass = e.FeeFinancialClass, ClassificationConflict = false, FinancialClass = e.FeeFinancialClass, Provenance = null,
                SignedAmount = -(e.ConfirmedFee - (db.CardSettlementFeeEvidence
                    .Where(p => p.BatchId == e.BatchId && p.Sequence < e.Sequence)
                    .OrderByDescending(p => p.Sequence).Select(p => (decimal?)p.ConfirmedFee).FirstOrDefault()
                    ?? e.Batch.ConfirmedFee ?? 0m))
            });
        // Retained Draft counts are candidates. Only persisted non-Draft official values count, including
        // migrated history without an attempt. The stock/closing total itself is never summed as a result.
        var variances = db.DailyCloseItems.AsNoTracking()
            .Where(i => i.Active && i.ProductId == varianceProductId && i.DailyClose.Active &&
                i.DailyClose.BranchId == branchId && i.DailyClose.Status != DailyCloseStatus.Draft)
            .Select(i => new FinancialResultEntry
            {
                AccountName = i.DailyClose.Account.Name, AccountActive = i.DailyClose.Account.Active,
                OfficialAttemptId = i.DailyClose.SubmittedAttemptId, SourceHistoryComplete = i.DailyClose.SubmittedAttemptId != null,
                TransactionId = null, BankReceiptId = null,
                SourceId = i.DailyCloseId, SourceKind = FinancialResultSourceKind.OfficialSessionVariance, AccountId = i.DailyClose.AccountId,
                SessionId = i.DailyCloseId, SourceDate = i.DailyClose.Date, PostedOn = i.DailyClose.Date,
                SnapshotFinancialClass = null, ClassificationConflict = false, FinancialClass = FinancialClass.CashDifference, Provenance = null, SignedAmount = i.Value
            });
        // The subledger's custody/principal movements are internal asset movements, not another sale.
        // No matching receipt or statement row is added again; migrated PaidAt is reconstruction, not new money.
        var eventMovements = db.SettlementEvents.AsNoTracking().Where(e => e.BranchId == branchId && e.Active &&
            (e.Kind == SettlementEventKind.CustodyMoved || e.Kind == SettlementEventKind.ChequeDeposited ||
             e.Kind == SettlementEventKind.ChequeReturned || e.Kind == SettlementEventKind.ProviderSettled ||
             e.Kind == SettlementEventKind.ChequeConvertedToFiado))
            .Select(e => new
            {
                Event = e,
                AccountId = e.CustodyToAccountId ?? (Guid?)e.BankReceipt!.BankAccountId ?? e.ReceivableTransaction.AccountId
            })
            .Select(x => new FinancialResultEntry
            {
                SourceId = x.Event.Id,
                SourceKind = x.Event.Kind == SettlementEventKind.CustodyMoved ? FinancialResultSourceKind.ChequeCustody :
                    x.Event.Kind == SettlementEventKind.ChequeDeposited ? FinancialResultSourceKind.ChequeDeposit :
                    x.Event.Kind == SettlementEventKind.ChequeReturned ? FinancialResultSourceKind.ChequeReturn :
                    x.Event.Kind == SettlementEventKind.ChequeConvertedToFiado ? FinancialResultSourceKind.ChequeConversion : FinancialResultSourceKind.ProviderPrincipalSettlement,
                TransactionId = x.Event.ReceivableTransactionId, BankReceiptId = x.Event.BankReceiptId,
                AccountId = x.AccountId,
                AccountName = db.Accounts.Where(a => a.Id == x.AccountId).Select(a => a.Name).FirstOrDefault(),
                AccountActive = db.Accounts.Where(a => a.Id == x.AccountId).Select(a => (bool?)a.Active).FirstOrDefault(),
                SessionId = x.Event.ReceivableTransaction.SessionId, OfficialAttemptId = null,
                SourceDate = x.Event.OccurredOn, PostedOn = x.Event.PostedOn,
                FinancialClass = FinancialClass.InternalTransfer, SnapshotFinancialClass = null,
                ClassificationConflict = false, Provenance = null,
                SourceHistoryComplete = x.Event.Provenance == SettlementEventProvenance.Command,
                SignedAmount = x.Event.Kind == SettlementEventKind.ChequeReturned ? -x.Event.Amount : x.Event.Amount
            });
        var window = transactions.Concat(initialFees).Concat(feeRevisions).Concat(variances).Concat(eventMovements)
            .Where(e => e.PostedOn >= from && e.PostedOn <= to);
        var classificationComplete = !await window.AnyAsync(e => e.FinancialClass == FinancialClass.Unclassified, ct);
        var query = window.Where(e => financialClass == null || e.FinancialClass == financialClass);
        var totals = await query.GroupBy(e => e.FinancialClass).Select(g => new FinancialClassTotal(
            g.Key, g.Sum(e => e.SignedAmount ?? 0m), g.Sum(e => Math.Abs(e.SignedAmount ?? 0m)),
            g.Count(), g.Count(e => e.SignedAmount == null),
            g.Count(e => e.Provenance == ClassificationProvenance.MigratedFromTypeAtUpgrade))).ToListAsync(ct);
        var count = await query.CountAsync(ct);
        var entries = await query.OrderBy(e => e.PostedOn).ThenBy(e => e.SourceKind).ThenBy(e => e.SourceId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var unresolved = await db.CardSettlementBatches.AsNoTracking().CountAsync(b => b.BranchId == branchId &&
            b.BankReceipt.PostedOn >= from && b.BankReceipt.PostedOn <= to && b.ConfirmedFee == null &&
            !db.CardSettlementFeeEvidence.Any(e => e.BatchId == b.Id && e.PostedOn <= to), ct);
        return new FinancialResultPage(totals, count, entries, unresolved, classificationComplete);
    }
}
