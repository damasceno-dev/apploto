using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Reopen;

public class ReopenDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
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

        await using var coordination = await accountCoordination.Acquire(
            branchUser.BranchId,
            closeKey.AccountId,
            ct);

        var close = await dailyClosesRepository.GetByIdAndBranchId(dailyCloseId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        workflowGuard.EnsureCanReopen(close, branchUser);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        draftTransition.ApplyReopen(close, branchClock.UtcNow(), branchUser.UserId);

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return close.ToResponse(cashVarianceProductId);
    }
}
