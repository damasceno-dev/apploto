using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.UpdateSegment;

public class UpdateTimeEntrySegmentUseCase(
    IAuthenticationService authenticationService,
    ITimeEntrySegmentsRepository timeEntrySegmentsRepository,
    LockDateGuard lockDateGuard,
    TimeEntrySegmentMutationService segmentMutationService,
    IBranchClock branchClock,
    IMonthLockCoordination monthLockCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTimeEntryJson> Execute(
        Guid segmentId,
        RequestUpdateTimeEntrySegmentJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);
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

        var clockIn = TimeEntrySegmentMutationService.AsUnspecified(request.ClockIn);
        var clockOut = TimeEntrySegmentMutationService.AsUnspecified(request.ClockOut);
        var candidateSegments = TimeEntrySegmentMutationService.ActiveSegments(parent)
            .Select(activeSegment => activeSegment.Id == segment.Id
                ? new TimeEntrySegmentRuleInput(activeSegment.Id, activeSegment.CreatedAt, clockIn, clockOut)
                : TimeEntrySegmentMutationService.ToRuleInput(activeSegment))
            .ToList();
        TimeEntrySegmentMutationService.EnsureSegmentRules(parent.Date, candidateSegments);

        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);

        segment.ClockIn = clockIn;
        segment.ClockOut = clockOut;
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

    private static void Validate(RequestUpdateTimeEntrySegmentJson request)
    {
        var result = new UpdateTimeEntrySegmentFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }

}
