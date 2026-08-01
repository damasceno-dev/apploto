using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.VariancePreview;

/// <summary>
/// Computes the close variance from the caller's unsaved candidate values. Every entity query is
/// no-tracking and the use case deliberately has no <see cref="IUnitOfWork"/> dependency, so a
/// preview cannot mutate or commit persisted state by construction.
/// </summary>
public class PreviewDailyCloseVarianceUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IProductsRepository productsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    LockDateGuard lockDateGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    ICashVarianceCalculator cashVarianceCalculator)
{
    public async Task<ResponseDailyCloseVariancePreviewJson> Execute(
        Guid dailyCloseId,
        RequestDailyCloseVariancePreviewJson request)
    {
        var items = Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var close = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(
            dailyCloseId,
            branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, close.AccountId);
        workflowGuard.EnsureCanSubmit(close, branchUser, callerOperator);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        var payloadProductIds = items.Select(item => item.ProductId).ToList();
        var resolvedProducts = await productsRepository.ListActiveByIdsAndBranchIdAsNoTracking(
            payloadProductIds,
            branchUser.BranchId);

        if (resolvedProducts.Count != payloadProductIds.Distinct().Count())
            throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_NOT_FOUND);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);

        if (payloadProductIds.Contains(cashVarianceProductId))
            throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN]);

        var cashVariance = await cashVarianceCalculator.CalculateCandidateAsync(
            branchUser.BranchId,
            close.AccountId,
            close.Date,
            items,
            cashVarianceProductId,
            CancellationToken.None);

        return new ResponseDailyCloseVariancePreviewJson
        {
            CashVariance = cashVariance
        };
    }

    /// <summary>
    /// Validates the payload and hands back the candidate items. Returning them keeps the
    /// validator's <c>NotNull</c> rule and the single null-forgiving assertion side by side,
    /// instead of repeating <c>request.Items!</c> at every use site.
    /// </summary>
    private static IReadOnlyList<RequestUpsertDailyCloseItemJson> Validate(
        RequestDailyCloseVariancePreviewJson request)
    {
        var result = new PreviewDailyCloseVarianceFluentValidation().Validate(request);
        return result.IsValid is false ? 
            throw new OnValidationException([.. result.Errors.Select(error => error.ErrorMessage)]) 
            : 
            request.Items!;
    }
}
