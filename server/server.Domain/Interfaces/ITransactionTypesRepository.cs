using server.Domain.Entities.Enums;
using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITransactionTypesRepository
{
    Task AddRange(IEnumerable<TransactionType> transactionTypes);
    Task Add(TransactionType transactionType);
    Task<TransactionType?> GetActiveByIdWithCategoryAndBranchId(Guid id, Guid branchId);
    Task<TransactionType?> GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<TransactionType?> GetActiveByIdAndBranchIdWithCategoryAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionType>> ListActiveByBranchIdAsNoTracking(Guid branchId);

    /// <summary>The branch's active system-owned counterpart type for <paramref name="role"/>, with its category.</summary>
    Task<TransactionType?> GetActiveSystemTypeByBranchIdAsNoTracking(
        Guid branchId, TransactionTypeSystemRole role, CancellationToken ct = default);

    /// <summary>True when the category classifies a system-owned type (such a category is read-only).</summary>
    Task<bool> CategoryHostsSystemTypeAsNoTracking(Guid categoryId, CancellationToken ct = default);
    Task<bool> ExistsActiveByCategoryIdAndName(Guid categoryId, string name, Guid? exceptId = null);

    /// <summary>
    /// True when any of the branch's types, active or retired, carries a settlement instrument (M11 Phase 5), so
    /// retiring the last classified type never hides the rows it already classified.
    /// </summary>
    Task<bool> ExistsWithSettlementInstrumentByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// The monthly financial roles (M11 Phase 6) carried by at least one of the branch's types, active or retired;
    /// <c>None</c> is never returned.
    /// </summary>
    Task<IReadOnlyList<MonthlyFinancialRole>> ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(
        Guid branchId, CancellationToken ct = default);
}
