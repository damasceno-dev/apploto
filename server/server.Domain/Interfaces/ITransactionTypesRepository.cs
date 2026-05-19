using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITransactionTypesRepository
{
    Task AddRange(IEnumerable<TransactionType> transactionTypes);
    Task Add(TransactionType transactionType);
    Task<TransactionType?> GetActiveByIdWithCategoryAndBranchId(Guid id, Guid branchId);
    Task<TransactionType?> GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<TransactionType?> GetActiveByIdAndBranchIdWithCategoryAsNoTracking(Guid id, Guid branchId);
    Task<IReadOnlyList<TransactionType>> ListActiveByBranchIdAsNoTracking(Guid branchId);
    Task<bool> ExistsActiveByCategoryIdAndName(Guid categoryId, string name, Guid? exceptId = null);
}
