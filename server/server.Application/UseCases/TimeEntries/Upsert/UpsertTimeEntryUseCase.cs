using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.Upsert;

public class UpsertTimeEntryUseCase(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    IOperatorsRepository operatorsRepository,
    ITimeEntriesRepository timeEntriesRepository,
    ISettingsRepository settingsRepository,
    IHolidaysRepository holidaysRepository,
    ITimeEntryWritePermissionGuard permissionGuard,
    ITimeEntryCalculationService calculationService,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTimeEntryJson> Execute(RequestUpsertTimeEntryJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        // Resolve caller's linked operator (null for Manager/Admin without a terminal,
        // and for Members the guard's MissingLinkedOperator outcome translates to 403).
        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        // Branch consistency on the target operator. Cross-branch or inactive operator → 404.
        var targetOperator = await operatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(request.OperatorId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_OPERATOR_NOT_FOUND);

        // Permission guard returns an explicit outcome — translate into the user-facing exception.
        var outcome = permissionGuard.Evaluate(
            branchUser,
            callerOperator,
            request.OperatorId,
            request.Date,
            request.Status,
            branchClock.UtcNow());

        ApplyPermissionOutcome(outcome);

        // Status-relative validation lives in the use case (not the validator) per the M3
        // shape-vs-business-rule split.
        await EnsureStatusRelativeRulesAsync(request, branchUser.BranchId);

        var existing = await timeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(branchUser.BranchId, request.OperatorId, request.Date);

        // Setting is bootstrapped atomically by CreateBranch, so absence here means the
        // branch is in a corrupt state — surface it as a programmer error rather than a 404.
        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchUser.BranchId)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        (decimal totalHours, decimal balanceHours) hours;
        if (request is { Status: TimeEntryStatus.Present, ClockOut: null })
        {
            // Partial Present (open shift on the current branch-local day): live-running estimate.
            // The persisted value is a last-write checkpoint; the read path recomputes on every call.
            var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());
            hours = calculationService.CalculateLiveRunning(
                request.ClockIn!.Value,
                request.Date,
                branchLocalNow,
                setting.DailyTargetHours,
                setting.LunchDeductionOver6H,
                setting.LunchDeductionOver4H);
        }
        else
        {
            hours = calculationService.Calculate(
                request.Status,
                request.ClockIn,
                request.ClockOut,
                setting.DailyTargetHours,
                setting.LunchDeductionOver6H,
                setting.LunchDeductionOver4H);
        }
        var (totalHours, balanceHours) = hours;

        // One captured instant — used for both the audit timestamp and any future
        // "same-instant" comparisons. UpsertTimeEntry has no other workflow timestamps,
        // but the convention matches DailyClose and Transaction mutation flows.
        var now = branchClock.UtcNow();

        TimeEntry entry;
        if (existing is null)
        {
            entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                Date = request.Date,
                OperatorId = request.OperatorId,
                BranchId = branchUser.BranchId,
                ClockIn = request.ClockIn,
                ClockOut = request.ClockOut,
                Status = request.Status,
                TotalHours = totalHours,
                BalanceHours = balanceHours,
                UpdatedAt = now,
                UpdatedByUserId = branchUser.UserId
            };
            await timeEntriesRepository.Add(entry);
        }
        else
        {
            existing.ClockIn = request.ClockIn;
            existing.ClockOut = request.ClockOut;
            existing.Status = request.Status;
            existing.TotalHours = totalHours;
            existing.BalanceHours = balanceHours;
            existing.UpdatedAt = now;
            existing.UpdatedByUserId = branchUser.UserId;
            entry = existing;
        }

        await unitOfWork.Commit();

        return entry.ToResponse(targetOperator.Name);
    }

    private static void ApplyPermissionOutcome(TimeEntryWritePermissionOutcome outcome)
    {
        switch (outcome)
        {
            case TimeEntryWritePermissionOutcome.Elevated:
            case TimeEntryWritePermissionOutcome.SelfSameDayPresent:
                return;
            case TimeEntryWritePermissionOutcome.MissingLinkedOperator:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
            case TimeEntryWritePermissionOutcome.NotOwnOperator:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
            case TimeEntryWritePermissionOutcome.OlderDayMember:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_WRITE_REQUIRES_SAME_DAY);
            case TimeEntryWritePermissionOutcome.MemberNonPresent:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_MEMBER_ONLY_PRESENT);
            default:
                throw new InvalidOperationException($"Unexpected {nameof(TimeEntryWritePermissionOutcome)}: {outcome}");
        }
    }

    private async Task EnsureStatusRelativeRulesAsync(RequestUpsertTimeEntryJson request, Guid branchId)
    {
        // ClockOut without ClockIn is invalid for any status — fires before the status-specific checks,
        // so the message is consistent regardless of whether Present or non-Present.
        if (request.ClockIn is null && request.ClockOut is not null)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN]);

        // Only Present accepts clocks — ClockIn is required; ClockOut may be null (open shift).
        // Non-Present statuses are full-day entries whose hours derive from the status itself,
        // so clocks would be meaningless; both must be null.
        if (request.Status is TimeEntryStatus.Present)
        {
            if (request.ClockIn is null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN]);
        }
        else
        {
            if (request.ClockIn is not null || request.ClockOut is not null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_CLOCKS]);
        }

        switch (request.Status)
        {
            // Calendar-aware status checks.
            case TimeEntryStatus.Sunday when request.Date.DayOfWeek is not DayOfWeek.Sunday:
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE]);
            case TimeEntryStatus.Holiday:
            {
                var holiday = await holidaysRepository.GetActiveByBranchIdAndDate(branchId, request.Date);
                if (holiday is null)
                {
                    throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY]);
                }

                break;
            }
            case TimeEntryStatus.Sunday:  // Sunday on a Sunday date — the `when` guard above filtered out the wrong-day case.
            case TimeEntryStatus.Present:
            case TimeEntryStatus.DayOff:
            case TimeEntryStatus.Vacation:
            case TimeEntryStatus.JustifiedAbsence:
            case TimeEntryStatus.UnjustifiedAbsence:
                break;
            default:
                throw new InvalidOperationException($"Unexpected {nameof(TimeEntryStatus)}: {request.Status}");
        }
    }

    private static void Validate(RequestUpsertTimeEntryJson request)
    {
        var result = new UpsertTimeEntryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
