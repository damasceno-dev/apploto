using System.Globalization;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Sessions.Count;
using server.Communication.Requests;
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
    SessionOpeningResolver openingResolver,
    SessionCountService countService,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Submits against the confirmed attempt (M9.6 Phase 4): the latest attempt must be the one named, still Current —
    /// same count, ledger and opening revisions and token, no Recontar pending — and the request's revisions must be the
    /// session's. Everything is checked after the cashbox lock and reload, and the tracked session version guards the
    /// commit, so a ledger, count or opening change cannot slip in between. The attempt's difference becomes the official
    /// snapshot; a recalculation that disagrees refuses the submission instead of overwriting it.
    /// </summary>
    public async Task<ResponseDailyCloseJson> Execute(
        Guid dailyCloseId,
        RequestSubmitSessionJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var validation = new SubmitSessionFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());
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

        if (close.MappingState != SessionMappingState.Ready)
            throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);

        var opening = await openingResolver.ResolveForCalculation(close, ct);
        var lockDate = await lockDateReader.Read(branchUser.BranchId, ct);
        lockDateGuard.EnsureNotLocked(
            close.Date,
            lockDate,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        if (close.ItemsFirstRecordedAt is null)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ITEMS_NOT_RECORDED);

        await dailyCloseLedgerGuard.EnsureNoOutstandingDraftTransactions(close, ct);

        // Submit-gap (§6.21): no uncounted, financially active predecessor session of the same cashbox may be
        // skipped, and unresolved legacy cashbox activity stays a blocker. Locked history is already settled.
        var uncountedActivityDate = await dailyClosesRepository
            .GetEarliestUncountedPredecessorActivityDateAsNoTracking(close, lockDate.Date, ct)
            ?? await transactionsRepository.GetEarliestUnsessionedCashboxActivityDateAsNoTracking(
                branchUser.BranchId,
                close.AccountId,
                lockDate.Date,
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

        var attempt = await countService.GetLatestAttempt(close, ct);
        if (attempt is null
            || attempt.Id != request.AttemptId
            || SessionCountService.StateOf(close, attempt) != SessionCandidateState.Current
            || request.CountRevision != close.CountRevision
            || request.LedgerRevision != close.LedgerRevision
            || request.OpeningRevision != close.OpeningRevision)
        {
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);
        }

        // A stale attempt is reported first (it names the reconfirmation path); otherwise any other change since the
        // caller's read is an ordinary stale write.
        if (close.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        var recalculated = await cashVarianceCalculator.CalculateWithOpeningAsync(
            close,
            opening,
            cashVarianceProductId,
            ct);
        if (recalculated != attempt.Difference)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);
        var cashVariance = attempt.Difference;

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
        close.SubmittedAttemptId = attempt.Id;
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
