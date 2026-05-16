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

namespace server.Application.UseCases.TimeEntries.AddSegment;

public class AddTimeEntrySegmentUseCase(
    IAuthenticationService authenticationService,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntrySegmentsRepository timeEntrySegmentsRepository,
    LockDateGuard lockDateGuard,
    TimeEntrySegmentMutationService segmentMutationService,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTimeEntryJson> Execute(Guid timeEntryId, RequestAddTimeEntrySegmentJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);
        TimeEntrySegmentMutationService.EnsureElevated(branchUser);

        var parent = await timeEntriesRepository.GetByIdAndBranchId(timeEntryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);

        TimeEntrySegmentMutationService.EnsureParentAcceptsSegments(parent);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            parent.Date,
            ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);

        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);
        var segment = new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            CreatedAt = utcNow,
            TimeEntryId = parent.Id,
            ClockIn = TimeEntrySegmentMutationService.AsUnspecified(request.ClockIn),
            ClockOut = TimeEntrySegmentMutationService.AsUnspecified(request.ClockOut)
        };

        var candidateSegments = TimeEntrySegmentMutationService.ActiveSegmentRuleInputs(parent);
        candidateSegments.Add(TimeEntrySegmentMutationService.ToRuleInput(segment));
        TimeEntrySegmentMutationService.EnsureSegmentRules(parent.Date, candidateSegments);

        parent.Segments.Add(segment);
        await timeEntrySegmentsRepository.Add(segment);
        await segmentMutationService.RecalculateParentTotalsAndStampAudit(
            parent,
            branchUser,
            utcNow,
            branchLocalNow);
        await unitOfWork.Commit();

        return parent.ToResponse(parent.Operator.Name);
    }

    private static void Validate(RequestAddTimeEntrySegmentJson request)
    {
        var result = new AddTimeEntrySegmentFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }

}
