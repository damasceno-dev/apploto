using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatorAccountsRepository
{
    Task Add(OperatorAccount operatorAccount);
    Task<OperatorAccount?> GetByOperatorIdAndAccountId(Guid operatorId, Guid accountId);
    Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorId(Guid operatorId);
}
