using server.Application.Services.Members;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Review;

public class GetDailyCloseReviewUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseDailyCloseReviewJson> Execute(Guid dailyCloseId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var dailyClose = await dailyClosesRepository
            .GetByIdAndBranchIdAsNoTracking(dailyCloseId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            if (memberScope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(
                    ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

            if (memberScope.AllowedAccountIds.Contains(dailyClose.AccountId) is false)
                throw new TokenWithoutPermissionException(
                    ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        var priorClose = await dailyClosesRepository
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                branchUser.BranchId,
                dailyClose.AccountId,
                dailyClose.Date);

        return dailyClose.ToReviewResponse(priorClose);
    }
}
