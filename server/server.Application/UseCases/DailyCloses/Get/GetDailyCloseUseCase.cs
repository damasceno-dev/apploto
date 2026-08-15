using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Get;

public class GetDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseDailyCloseJson> Execute(Guid dailyCloseId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var dailyClose = await dailyClosesRepository
            .GetByIdAndBranchIdAsNoTracking(dailyCloseId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

            if (memberScope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

            if (memberScope.AllowedAccountIds.Contains(dailyClose.AccountId) is false)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        return dailyClose.ToResponse(cashVarianceProductId);
    }
}
