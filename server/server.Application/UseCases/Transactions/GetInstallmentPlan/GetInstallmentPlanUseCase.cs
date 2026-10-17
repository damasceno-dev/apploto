using server.Application.Services.Members;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.GetInstallmentPlan;

/// <summary>
/// Reads one installment plan with its computed status, so a client opens a wholly Draft plan in
/// finalize mode and any other plan in per-row correction mode. Same scope as <c>GET /transaction/{id}</c>:
/// missing/cross-branch → 404; a Member needs an operator link and the plan's account in scope (403).
/// </summary>
public class GetInstallmentPlanUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseInstallmentPlanJson> Execute(Guid originTransactionId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        MemberAccountScope? memberScope = null;
        if (branchUser.Role is Role.Member)
        {
            memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
            if (memberScope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        var installments = await transactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
            originTransactionId,
            branchUser.BranchId,
            ct);
        if (installments.Count == 0)
            throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_FOUND);

        if (memberScope is not null && memberScope.AllowedAccountIds.Contains(installments[0].AccountId) is false)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);

        return installments.ToPlanResponse(originTransactionId);
    }
}
