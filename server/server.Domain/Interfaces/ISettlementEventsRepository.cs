using server.Domain.Entities;

namespace server.Domain.Interfaces;

/// <summary>Append-only receivable lifecycle events (M9.7 Phases 1–2).</summary>
public interface ISettlementEventsRepository
{
    Task Add(SettlementEvent settlementEvent, CancellationToken ct = default);

    /// <summary>Every event of the given receivables in the branch, ordered by receivable then sequence.</summary>
    Task<IReadOnlyList<SettlementEvent>> ListByReceivableIdsAndBranchIdAsNoTracking(
        IReadOnlyCollection<Guid> receivableTransactionIds,
        Guid branchId,
        CancellationToken ct = default);

    /// <summary>The deposit events (allocations) of one bank receipt, with whether each has been returned.</summary>
    Task<IReadOnlyList<SettlementEvent>> ListByBankReceiptIdAndBranchIdAsNoTracking(
        Guid bankReceiptId,
        Guid branchId,
        CancellationToken ct = default);

    /// <summary>Return events that reverse any of the given deposit events.</summary>
    Task<IReadOnlyList<SettlementEvent>> ListReturnsOfDepositEventsAsNoTracking(
        IReadOnlyCollection<Guid> depositEventIds,
        Guid branchId,
        CancellationToken ct = default);

    /// <summary>True when the receivable has any event, reconstructed or not.</summary>
    Task<bool> ExistsByReceivableIdAsNoTracking(Guid receivableTransactionId, CancellationToken ct = default);

    /// <summary>True when the receivable has any event appended by a command (not only a reconstructed one).</summary>
    Task<bool> ExistsCommandEventByReceivableIdAsNoTracking(Guid receivableTransactionId, CancellationToken ct = default);
}
