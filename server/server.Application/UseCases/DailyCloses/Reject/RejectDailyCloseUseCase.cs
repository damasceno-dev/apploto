using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Reject;

public class RejectDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseWorkflowGuard workflowGuard,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    ICashVarianceProductResolver cashVarianceProductResolver,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(
        Guid dailyCloseId,
        RequestRejectDailyCloseJson request,
        CancellationToken ct = default)
    {
        Validate(request);
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

        workflowGuard.EnsureCanReject(close, branchUser);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        var now = branchClock.UtcNow();
        close.Status = DailyCloseStatus.Rejected;
        close.RejectionReason = request.RejectionReason;
        close.ApprovedAt = null;
        close.ApprovedByUserId = null;
        close.ApprovedByUser = null;
        close.UpdatedAt = now;
        close.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return close.ToResponse(cashVarianceProductId);
    }

    private static void Validate(RequestRejectDailyCloseJson request)
    {
        var result = new RejectDailyCloseFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
