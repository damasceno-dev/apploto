using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public class MemberAccountScopeGuard(IOperatorAccountsRepository operatorAccountsRepository)
{
    public async Task EnsureMemberCanActOnAccount(Role role, Guid? callerOperatorId, Guid accountId)
    {
        if (role != Role.Member)
        {
            return;
        }

        if (callerOperatorId is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        var activeAccounts = await operatorAccountsRepository.ListActiveByOperatorIdAsNoTracking(callerOperatorId.Value);
        if (activeAccounts.Any(operatorAccount => operatorAccount.AccountId == accountId))
        {
            return;
        }

        throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }
}
