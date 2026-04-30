using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.Open;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.PutItems;

public class PutDailyCloseItemsUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    IProductsRepository productsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(Guid dailyCloseId, RequestPutDailyCloseItemsJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var close = await dailyClosesRepository.GetByIdAndBranchId(dailyCloseId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, close.AccountId);

        var outcome = workflowGuard.EnsureCanEditItems(close, branchUser, callerOperator);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        // One-shot product resolution — cross-branch or inactive products are absent from the result.
        var payloadProductIds = request.Items!.Select(i => i.ProductId).ToList();
        var resolvedProducts = await productsRepository
            .ListActiveByIdsAndBranchIdAsNoTracking(payloadProductIds, branchUser.BranchId);

        if (resolvedProducts.Count != payloadProductIds.Distinct().Count())
            throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_NOT_FOUND);

        // Reject any payload line that references the system-managed CashVariance product.
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);

        if (payloadProductIds.Any(id => id == cashVarianceProductId))
            throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN]);

        var now = branchClock.UtcNow();

        // Apply auto-transition based on the workflow outcome.
        switch (outcome)
        {
            case DailyCloseEditItemsOutcome.EditOnDraft:
                // No status transition — close is already Draft; nothing to do.
                break;

            case DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft:
                close.Status = DailyCloseStatus.Draft;
                break;

            case DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft:
                close.Status = DailyCloseStatus.Draft;
                close.SubmittedAt = null;
                break;

            default:
                throw new InvalidOperationException($"Unexpected {nameof(DailyCloseEditItemsOutcome)}: {outcome}");
        }

        // Upsert items. Updates mutate tracked entities in-place; inserts go through
        // IDailyCloseItemsRepository.Add so EF Core tracks them as EntityState.Added
        // and issues INSERT (not UPDATE).
        var productMap = resolvedProducts.ToDictionary(p => p.Id);
        var payloadProductIdSet = payloadProductIds.ToHashSet();

        // Soft-delete active items omitted from the payload (never touch the CashVariance row).
        foreach (var item in close.Items.Where(i =>
            i.Active &&
            payloadProductIdSet.Contains(i.ProductId) is false &&
            i.ProductId != cashVarianceProductId))
        {
            item.Active = false;
        }

        // Insert new or mutate existing items.
        foreach (var payloadItem in request.Items!)
        {
            var existing = close.Items.FirstOrDefault(i => i.Active && i.ProductId == payloadItem.ProductId);
            if (existing is not null)
            {
                existing.Value = payloadItem.Value;
            }
            else
            {
                // Use the repository's Add so EF Core tracks the entity as EntityState.Added
                // and issues INSERT. Adding directly to close.Items (the tracked navigation
                // collection) would leave EF Core uncertain whether the client-generated Guid
                // belongs to a new or existing row, causing it to issue UPDATE instead of INSERT.
                await dailyCloseItemsRepository.Add(new DailyCloseItem
                {
                    DailyCloseId = close.Id,
                    ProductId = payloadItem.ProductId,
                    Value = payloadItem.Value
                });
            }
        }

        // Stamp generic mutation audit from the single captured instant.
        close.UpdatedAt = now;
        close.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit();

        return close.ToResponse(productMap);
    }

    private static void Validate(RequestPutDailyCloseItemsJson request)
    {
        var result = new PutDailyCloseItemsFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
