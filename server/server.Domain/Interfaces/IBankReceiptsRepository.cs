using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

/// <summary>Pending bank receipts (M9.7 Phase 2.2); confirmation belongs to statement reconciliation (Phase 5).</summary>
public interface IBankReceiptsRepository
{
    Task<BankMovement?> GetMovementAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default);
    Task<BankReceiptReturnSource?> GetReturnSourceAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default);
    Task<CardSettlementBatch?> GetCardBatchAsNoTracking(Guid receiptId, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<CardSettlementFeeEvidence>> ListFeeEvidenceAsNoTracking(Guid batchId, Guid branchId, CancellationToken ct = default);
    Task Add(BankReceipt receipt, CancellationToken ct = default);
    Task<BankReceipt?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
}
