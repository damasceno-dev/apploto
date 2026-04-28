using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Members;

public class MemberAccountScopeGuard
{
    public void EnsureMemberCanActOnAccount(Role role, MemberAccountScope memberScope, Guid accountId)
    {
        if (role != Role.Member)
        {
            return;
        }

        if (memberScope.LinkedOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        if (memberScope.AllowedAccountIds.Contains(accountId))
        {
            return;
        }

        throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }
}
