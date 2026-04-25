using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public class MemberAccountScopeGuard
{
    public void EnsureMemberCanActOnAccount(Role role, MemberTransactionScope memberScope, Guid accountId)
    {
        if (role != Role.Member)
        {
            return;
        }

        if (memberScope.LinkedOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        if (memberScope.AllowedAccountIds.Contains(accountId))
        {
            return;
        }

        throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }
}
