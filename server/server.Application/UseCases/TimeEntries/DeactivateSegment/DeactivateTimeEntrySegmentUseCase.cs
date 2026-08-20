using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.DeactivateSegment;

public class DeactivateTimeEntrySegmentUseCase(
    IAuthenticationService authenticationService,
    ITimeEntrySegmentsRepository timeEntrySegmentsRepository,
    LockDateGuard lockDateGuard,
    TimeEntrySegmentMutationService segmentMutationService,
    IBranchClock branchClock,
    IMonthLockCoordination monthLockCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTimeEntryJson> Execute(Guid segmentId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        TimeEntrySegmentMutationService.EnsureElevated(branchUser);

        await using var coordination = await monthLockCoordination.TryAcquireShared(branchUser.BranchId, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);

        var segment = await timeEntrySegmentsRepository.GetActiveByIdAndBranchId(segmentId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
        var parent = segment.TimeEntry;

        TimeEntrySegmentMutationService.EnsureParentAcceptsSegments(parent);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            parent.Date,
            ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED,
            ct);

        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);

        segment.Active = false;
        segment.UpdatedAt = utcNow;
        segment.UpdatedByUserId = branchUser.UserId;

        await segmentMutationService.RecalculateParentTotalsAndStampAudit(
            parent,
            branchUser,
            utcNow,
            branchLocalNow);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return parent.ToResponse(parent.Operator.Name);
    }

}
