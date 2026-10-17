using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.FinalizeInstallmentPlan;

/// <summary>
/// Finalizes a cheque plan atomically (M7.7 Phase 7.6.2, gap Y): every expected Draft installment
/// becomes Active in one commit, or none does. The authoritative sibling set is resolved by
/// <c>OriginTransactionId</c> inside the branch under the plan account's coordination; the caller's
/// expected set and versions must match it exactly. A Mixed plan (Draft alongside Active and/or
/// Cancelled) finalizes its remaining Draft installments only with explicit confirmation.
/// </summary>
public class FinalizeInstallmentPlanUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    ITransactionMutationPermissionGuard transactionMutationPermissionGuard,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseInstallmentPlanJson> Execute(
        Guid originTransactionId,
        RequestFinalizeInstallmentPlanJson request,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var planKey = await transactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
            originTransactionId,
            branchUser.BranchId,
            ct);
        if (planKey.Count == 0)
            throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

        // Every installment of a plan shares the plan's account and business date (neither is editable),
        // so the account's coordination key serializes the whole sibling set.
        await using var coordination = await dailyCloseAccountCoordination.Acquire(
            branchUser.BranchId,
            planKey[0].AccountId,
            ct);

        var installments = await transactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchId(
            originTransactionId,
            branchUser.BranchId,
            ct);
        if (installments.Count == 0)
            throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_FOUND);

        var accountId = installments[0].AccountId;
        var date = installments[0].Date.Date;
        if (installments.Any(installment => installment.AccountId != accountId || installment.Date.Date != date))
            throw new InvalidOperationException($"Installment plan {originTransactionId} spans more than one account/date.");

        var drafts = installments.Where(installment => installment.Status == TransactionStatus.Draft).ToList();
        if (drafts.Count == 0)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_DRAFT);

        if (installments.ToPlanStatus() == InstallmentPlanStatus.Mixed && request.ConfirmMixedPlan is false)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED);

        var expected = request.Installments!.ToDictionary(row => row.TransactionId, row => row.Version);
        if (expected.Count != drafts.Count ||
            drafts.Any(draft => expected.TryGetValue(draft.Id, out var version) is false || version != draft.Version))
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        }

        if (branchUser.Role == Role.Member && memberScope.LinkedOperator is not null)
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, accountId);

        var utcNow = branchClock.UtcNow();
        foreach (var draft in drafts)
            transactionMutationPermissionGuard.EnsureAllowed(draft, branchUser.Role, memberScope.LinkedOperator, utcNow);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        await dailyCloseLedgerGuard.EnsureLedgerIsMutable(branchUser.BranchId, accountId, date, ct);

        foreach (var draft in drafts)
        {
            draft.Status = TransactionStatus.Active;
            draft.UpdatedAt = utcNow;
            draft.UpdatedByUserId = branchUser.UserId;
        }

        // A permitted per-row edit (which takes no account coordination) can still change an installment's
        // version between the check above and this save; report it with this command's own stale error.
        if (await unitOfWork.TryCommitOptimistic(ct) is false)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        await coordination.Complete(ct);

        return installments.ToPlanResponse(originTransactionId);
    }

    private static void Validate(RequestFinalizeInstallmentPlanJson request)
    {
        var result = new FinalizeInstallmentPlanFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
