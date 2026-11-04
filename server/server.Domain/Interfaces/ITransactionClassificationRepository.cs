using server.Domain.Entities;

namespace server.Domain.Interfaces;

/// <summary>
/// Frozen classification snapshots (M9.7 Phase 1.3). Candidates are Active rows of one type dated in an open window
/// whose snapshot differs from the type's current markers; applying a revision is the only way a snapshot changes.
/// </summary>
public interface ITransactionClassificationRepository
{
    Task<IReadOnlyList<Transaction>> ListReclassificationCandidatesAsNoTracking(
        Guid transactionTypeId,
        Guid branchId,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken ct = default);

    /// <summary>The rows as stored after a reclassification, in date order.</summary>
    Task<IReadOnlyList<Transaction>> ListReclassifiedRowsAsNoTracking(
        IReadOnlyCollection<Guid> transactionIds,
        Guid branchId,
        CancellationToken ct = default);

    Task AddRevisions(IReadOnlyCollection<TransactionClassificationRevision> revisions, CancellationToken ct = default);

    /// <summary>
    /// Writes one revision's snapshot on its row when the row is still at <paramref name="expectedVersion"/>; false
    /// when the row changed meanwhile. Runs inside the caller's transaction, after the revisions were saved.
    /// </summary>
    Task<bool> TryApplyRevision(
        TransactionClassificationRevision revision,
        uint expectedVersion,
        DateTime classifiedAt,
        CancellationToken ct = default);
}
