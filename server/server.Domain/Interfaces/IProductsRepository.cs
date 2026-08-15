using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IProductsRepository
{
    Task AddRange(IEnumerable<Product> products);
    Task Add(Product product);

    Task<Product?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Product?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);

    Task<IReadOnlyList<Product>> ListActiveByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all active products whose ids are in <paramref name="productIds"/> and
    /// that belong to the given branch. Products missing from the result were either
    /// not found, deactivated, or belong to a different branch.
    /// </summary>
    Task<IReadOnlyList<Product>> ListActiveByIdsAndBranchIdAsNoTracking(
        IEnumerable<Guid> productIds,
        Guid branchId,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up the active product with the exact display name in the given branch.
    /// Returns <c>null</c> when no match exists (bootstrap defect; seed was not run).
    /// </summary>
    Task<Product?> GetActiveByBranchIdAndNameAsNoTracking(Guid branchId, string name, CancellationToken ct = default);

    Task<bool> ExistsActiveByBranchIdAndName(Guid branchId, string name, Guid? exceptId = null);
}
