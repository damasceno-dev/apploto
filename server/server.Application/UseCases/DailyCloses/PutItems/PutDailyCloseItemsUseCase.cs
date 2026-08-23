using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
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
    IDailyCloseDraftTransition draftTransition,
    IDailyCloseSuccessorInvalidator successorInvalidator,
    ICashVarianceProductResolver cashVarianceProductResolver,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponsePutDailyCloseItemsJson> Execute(
        Guid dailyCloseId,
        RequestPutDailyCloseItemsJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var items = Validate(request);
        ct.ThrowIfCancellationRequested();

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var closeKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(
            dailyCloseId,
            branchUser.BranchId,
            ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

        await using var coordination = await accountCoordination.Acquire(
            branchUser.BranchId,
            closeKey.AccountId,
            ct);

        var close = await dailyClosesRepository.GetByIdAndBranchId(dailyCloseId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, close.AccountId);
        var outcome = workflowGuard.EnsureCanEditItems(close, branchUser, memberScope.LinkedOperator);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);

        if (close.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_STALE_WRITE);

        var payloadProductIds = items.Select(item => item.ProductId).ToList();
        var resolvedProducts = await productsRepository.ListActiveByIdsAndBranchIdAsNoTracking(
            payloadProductIds,
            branchUser.BranchId,
            ct);

        if (resolvedProducts.Count != payloadProductIds.Distinct().Count())
            throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_NOT_FOUND);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        if (payloadProductIds.Contains(cashVarianceProductId))
            throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN]);

        var beforeMap = close.Items
            .Where(item => item.Active && item.ProductId != cashVarianceProductId)
            .ToDictionary(item => item.ProductId, item => item.Value);
        var afterMap = items.ToDictionary(item => item.ProductId, item => item.Value);
        var firstEligibilityChange = close.ItemsFirstRecordedAt is null;
        var countedInputChanged = firstEligibilityChange || MapsDiffer(beforeMap, afterMap);
        var now = branchClock.UtcNow();

        if (outcome == DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft)
            draftTransition.ApplyRejectedCorrection(close, now, branchUser.UserId);

        var payloadProductIdSet = payloadProductIds.ToHashSet();
        foreach (var item in close.Items.Where(item =>
                     item.Active &&
                     item.ProductId != cashVarianceProductId &&
                     !payloadProductIdSet.Contains(item.ProductId)))
        {
            item.Active = false;
        }

        foreach (var payloadItem in items)
        {
            var existing = close.Items.FirstOrDefault(item => item.Active && item.ProductId == payloadItem.ProductId);
            if (existing is not null)
            {
                existing.Value = payloadItem.Value;
                continue;
            }

            await dailyCloseItemsRepository.Add(new DailyCloseItem
            {
                DailyCloseId = close.Id,
                ProductId = payloadItem.ProductId,
                Value = payloadItem.Value
            }, ct);
        }

        if (request.Notes is not null)
            close.Notes = request.Notes.Length == 0 ? null : request.Notes;

        if (firstEligibilityChange)
        {
            close.ItemsFirstRecordedAt = now;
            close.RecordedByUserId = branchUser.UserId;
            close.RecordedByOperatorId = memberScope.LinkedOperator?.Id;
        }

        close.UpdatedAt = now;
        close.UpdatedByUserId = branchUser.UserId;

        DailyCloseSuccessorInvalidation? invalidation = null;
        if (countedInputChanged)
        {
            invalidation = await successorInvalidator.InvalidateNextEligible(
                close,
                now,
                branchUser.UserId,
                ct);
        }

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        var productMap = resolvedProducts.ToDictionary(product => product.Id);
        return new ResponsePutDailyCloseItemsJson
        {
            DailyClose = close.ToResponse(
                productMap,
                cashVarianceProductId,
                firstEligibilityChange ? branchUser.User : null,
                firstEligibilityChange ? memberScope.LinkedOperator : null),
            AffectedSuccessor = invalidation is null
                ? null
                : new ResponseAffectedDailyCloseSuccessorJson
                {
                    DailyCloseId = invalidation.DailyClose.Id,
                    Date = invalidation.DailyClose.Date,
                    PreviousStatus = invalidation.PreviousStatus,
                    NewStatus = invalidation.DailyClose.Status,
                    OpeningRecheckRequiredAt = invalidation.OpeningRecheckRequiredAt
                }
        };
    }

    private static bool MapsDiffer(
        IReadOnlyDictionary<Guid, decimal> before,
        IReadOnlyDictionary<Guid, decimal> after)
    {
        return before.Count != after.Count ||
               before.Any(pair => !after.TryGetValue(pair.Key, out var value) || value != pair.Value);
    }

    private static IReadOnlyList<RequestUpsertDailyCloseItemJson> Validate(RequestPutDailyCloseItemsJson request)
    {
        var result = new PutDailyCloseItemsFluentValidation().Validate(request);
        return result.IsValid
            ? request.Items!
            : throw new OnValidationException([.. result.Errors.Select(e => e.ErrorMessage)]);
    }
}
