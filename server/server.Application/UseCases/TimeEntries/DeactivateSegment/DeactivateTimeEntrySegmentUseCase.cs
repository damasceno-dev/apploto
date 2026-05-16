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
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTimeEntryJson> Execute(Guid segmentId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        TimeEntrySegmentMutationService.EnsureElevated(branchUser);

        var segment = await timeEntrySegmentsRepository.GetActiveByIdAndBranchId(segmentId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
        var parent = segment.TimeEntry;

        TimeEntrySegmentMutationService.EnsureParentAcceptsSegments(parent);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            parent.Date,
            ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);

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
        await unitOfWork.Commit();

        return parent.ToResponse(parent.Operator.Name);
    }

}
