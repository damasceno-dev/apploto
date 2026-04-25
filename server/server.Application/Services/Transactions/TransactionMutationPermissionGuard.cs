using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Transactions;

/// <summary>
/// Permission decision shared by Update / Finalize / Cancel: elevated roles always pass;
/// non-elevated callers must be the recording operator and must act on the same local
/// business day as the transaction date.
/// </summary>
public sealed class TransactionMutationPermissionGuard(IBranchClock branchClock) : ITransactionMutationPermissionGuard
{
    public void EnsureAllowed(Transaction transaction, Role role, Operator? callerOperator, DateTime utcNow)
    {
        if (role is Role.Manager or Role.Admin)
        {
            return;
        }

        if (callerOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        if (callerOperator.Id != transaction.RecordedByOperatorId)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        }

        if (branchClock.IsSameLocalDay(transaction.Date, utcNow) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        }
    }
}
