using server.Domain.Entities;

namespace server.Domain.Interfaces;

/// <summary>Pending bank receipts (M9.7 Phase 2.2); confirmation belongs to statement reconciliation (Phase 5).</summary>
public interface IBankReceiptsRepository
{
    Task Add(BankReceipt receipt, CancellationToken ct = default);
    Task<BankReceipt?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
}
