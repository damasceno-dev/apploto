using System.Globalization;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Submit;

public class SubmitDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    ILockDateReader lockDateReader,
    LockDateGuard lockDateGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    ICashVarianceCalculator cashVarianceCalculator,
    IBranchClock branchClock,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(Guid dailyCloseId, CancellationToken ct = default)
    {
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
        workflowGuard.EnsureCanSubmit(close, branchUser, memberScope.LinkedOperator);

        var openingSource = await dailyClosesRepository
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                branchUser.BranchId,
                close.AccountId,
                close.Date,
                ct);
        var lockDate = await lockDateReader.Read(branchUser.BranchId, ct);
        lockDateGuard.EnsureNotLocked(
            close.Date,
            lockDate,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        if (close.ItemsFirstRecordedAt is null)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ITEMS_NOT_RECORDED);

        await dailyCloseLedgerGuard.EnsureNoOutstandingDraftTransactions(
            branchUser.BranchId,
            close.AccountId,
            close.Date,
            ct);

        var rangeStartExclusive = openingSource?.Date.Date ?? DateTime.MinValue;
        if (lockDate.Date > rangeStartExclusive)
            rangeStartExclusive = lockDate.Date;

        var uncountedActivityDate = await transactionsRepository
            .GetEarliestUncountedActivityDateByAccountAsNoTracking(
                branchUser.BranchId,
                close.AccountId,
                rangeStartExclusive,
                close.Date.Date,
                ct);

        if (uncountedActivityDate is { } blockingDate)
        {
            var formattedDate = blockingDate.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
            throw new ConflictException(string.Format(
                CultureInfo.InvariantCulture,
                ResourcesErrorMessages.DAILYCLOSE_PRIOR_DAY_NOT_COUNTED,
                formattedDate));
        }

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        var cashVariance = await cashVarianceCalculator.CalculateWithOpeningSourceAsync(
            branchUser.BranchId,
            close.AccountId,
            close.Date,
            close.Id,
            cashVarianceProductId,
            openingSource,
            ct);

        var existingVariance = close.Items.FirstOrDefault(item =>
            item.Active && item.ProductId == cashVarianceProductId);
        if (existingVariance is null)
        {
            var varianceItem = new DailyCloseItem
            {
                DailyCloseId = close.Id,
                ProductId = cashVarianceProductId,
                Value = cashVariance
            };
            await dailyCloseItemsRepository.Add(varianceItem, ct);
        }
        else
        {
            existingVariance.Value = cashVariance;
        }

        var now = branchClock.UtcNow();
        close.SubmittedByUserId = branchUser.UserId;
        close.SubmittedByOperatorId = memberScope.LinkedOperator?.Id;
        close.Status = DailyCloseStatus.Submitted;
        close.SubmittedAt = now;
        close.ApprovedAt = null;
        close.ApprovedByUserId = null;
        close.ApprovedByUser = null;
        close.RejectionReason = null;
        close.OpeningRecheckRequiredAt = null;
        close.OpeningRecheckTriggeredByDailyCloseId = null;
        close.OpeningRecheckTriggeredByDailyClose = null;
        close.OpeningRecheckTriggeredByUserId = null;
        close.UpdatedAt = now;
        close.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return close.ToResponse(
            cashVarianceProductId,
            branchUser.User,
            memberScope.LinkedOperator);
    }
}
