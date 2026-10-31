using server.Application.Services.Sessions;
using server.Application.Services.DailyCloses;
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
    IProductsRepository productsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionOpeningResolver openingResolver,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    server.Application.UseCases.Sessions.SessionCountProjector countProjector)
{
    public async Task<ResponseDailyCloseReviewJson> Execute(
        Guid dailyCloseId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var dailyClose = await dailyClosesRepository
            .GetByIdAndBranchIdAsNoTracking(dailyCloseId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        if (branchUser.Role is Role.Member)
        {
            if (memberScope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(
                    ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

            if (memberScope.AllowedAccountIds.Contains(dailyClose.AccountId) is false)
                throw new TokenWithoutPermissionException(
                    ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        var opening = await openingResolver.ResolveForCalculation(dailyClose, ct);
        var discrepancy = await discrepanciesRepository.GetBySessionIdAsNoTracking(branchUser.BranchId, dailyClose.Id, ct);
        var activeProducts = await productsRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId, ct);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        var (count, actions) = await countProjector.Build(dailyClose, branchUser, memberScope, ct);

        return dailyClose.ToReviewResponse(opening, discrepancy, activeProducts, cashVarianceProductId, count, actions);
    }
}
