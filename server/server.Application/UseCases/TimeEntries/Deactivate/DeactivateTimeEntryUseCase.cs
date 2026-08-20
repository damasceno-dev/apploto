using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.Deactivate;

public class DeactivateTimeEntryUseCase(
    IAuthenticationService authenticationService,
    ITimeEntriesRepository timeEntriesRepository,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    IMonthLockCoordination monthLockCoordination,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Soft-deactivates a TimeEntry. Manager/Admin only — Members never reach this path.
    /// The row stays in place so the filtered unique index <c>(BranchId, OperatorId, Date)
    /// WHERE Active = true</c> allows the operator (or a manager) to re-upsert a fresh
    /// active row for the same key later.
    /// </summary>
    public async Task<ResponseTimeEntryJson> Execute(Guid timeEntryId, CancellationToken ct = default)
    {
        if (timeEntryId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        await using var coordination = await monthLockCoordination.TryAcquireShared(branchUser.BranchId, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);

        // Repo's GetByIdAndBranchId already filters Active = true, so already-inactive
        // rows surface as 404 — same convention as M2 Operator deactivate.
        var timeEntry = await timeEntriesRepository.GetByIdAndBranchId(timeEntryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            timeEntry.Date,
            ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED,
            ct);

        var now = branchClock.UtcNow();

        timeEntry.Active = false;
        timeEntry.UpdatedAt = now;
        timeEntry.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return timeEntry.ToResponse(timeEntry.Operator.Name);
    }
}
