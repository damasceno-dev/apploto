using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Recall;

public class RecallDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    IDailyCloseDraftTransition draftTransition,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    ICashVarianceProductResolver cashVarianceProductResolver,
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
        workflowGuard.EnsureCanRecall(close, branchUser, memberScope.LinkedOperator);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        draftTransition.ApplyRecall(close, branchClock.UtcNow(), branchUser.UserId);

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return close.ToResponse(cashVarianceProductId);
    }
}
