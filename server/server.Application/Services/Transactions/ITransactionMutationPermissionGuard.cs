using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Transactions;

public interface ITransactionMutationPermissionGuard
{
    void EnsureAllowed(Transaction transaction, Role role, Operator? callerOperator, DateTime utcNow);
}
