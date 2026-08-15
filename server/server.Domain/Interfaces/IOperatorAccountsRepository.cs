using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatorAccountsRepository
{
    Task Add(OperatorAccount operatorAccount);
    Task<OperatorAccount?> GetByOperatorIdAndAccountId(Guid operatorId, Guid accountId);
    Task<OperatorAccount?> GetActivePrimaryByOperatorId(Guid operatorId);
    Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorId(Guid operatorId);
    Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorIdAsNoTracking(Guid operatorId, CancellationToken ct = default);
    Task<IReadOnlyList<OperatorAccount>> ListActiveByAccountId(Guid accountId);
    Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorIdWithAccount(Guid operatorId);
}
