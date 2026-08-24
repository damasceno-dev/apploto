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

namespace server.Application.UseCases.Reports.TimeEntryBalance;

public class GetTimeEntryBalanceSummaryUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    ITimeEntryCalculationService calculationService,
    IBranchClock branchClock,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseTimeEntryBalanceSummaryJson> Execute(RequestTimeEntryBalanceSummaryJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        // Resolve the read into one of three shapes: a single named/own operator,
        // an empty result (unlinked Member), or the branch-wide roll-up
        // (Manager/Admin who named neither an operator nor Mine).
        Operator? targetOperator = null;
        var branchWide = false;

        if (branchUser.Role is Role.Member)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            // Empty-scope short-circuit: a Member with no linked operator has nothing
            // to summarize, regardless of Mine or OperatorId. A Member never reaches
            // the branch-wide path.
            if (memberScope.LinkedOperator is null)
                return EmptyResponse(request);

            // Members cannot read another operator's balance. Mine = true, an omitted
            // OperatorId, and an explicit own OperatorId all resolve to the linked operator.
            if (request.OperatorId is { } explicitOperatorId && explicitOperatorId != memberScope.LinkedOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);

            targetOperator = memberScope.LinkedOperator;
        }
        else if (request.Mine || request.OperatorId is not null)
        {
            Guid targetOperatorId;
            if (request.Mine)
            {
                var ownScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
                targetOperatorId = ownScope.LinkedOperator?.Id
                    ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
            }
            else
            {
                targetOperatorId = request.OperatorId!.Value;
            }

            targetOperator = await operatorsRepository
                    .GetActiveByIdAndBranchIdAsNoTracking(targetOperatorId, branchUser.BranchId)
                ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        }
        else
        {
            // Manager/Admin who named neither an operator nor Mine → branch-wide roll-up.
            branchWide = true;
        }

        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);

        // Capture the branch-local clock once for the whole report (NOT per row, NOT per
        // operator) so every operator's live-running recompute shares one consistent "now".
        var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());

        List<ResponseTimeEntryBalanceOperatorJson> operators;
        if (branchWide)
        {
            var entries = await timeEntriesRepository.ListByBranchIdAndDateRangeAsNoTracking(
                branchUser.BranchId, request.DateFrom, request.DateTo);

            // Roll-up membership: every active operator — zero-entry rows included, with or
            // without a login link, so payroll sees the employee who never clocked in — plus
            // any operator that has entries in the window even after being deactivated.
            var entriesByOperator = entries
                .GroupBy(entry => entry.OperatorId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<TimeEntry>)group.ToList());

            var activeOperators = await operatorsRepository.ListActiveByBranchId(branchUser.BranchId);
            var operatorNamesById = activeOperators.ToDictionary(op => op.Id, op => op.Name);
            foreach (var group in entriesByOperator)
                operatorNamesById.TryAdd(group.Key, group.Value[0].Operator.Name);

            operators =
            [
                .. operatorNamesById
                    .Select(pair => BuildOperatorSummary(
                        pair.Key,
                        pair.Value,
                        entriesByOperator.GetValueOrDefault(pair.Key, []),
                        policies,
                        branchLocalNow))
                    .OrderBy(summary => summary.OperatorName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(summary => summary.OperatorId)
            ];
        }
        else
        {
            var entries = await timeEntriesRepository.ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
                branchUser.BranchId, targetOperator!.Id, request.DateFrom, request.DateTo);

            operators = [BuildOperatorSummary(targetOperator.Id, targetOperator.Name, entries, policies, branchLocalNow)];
        }

        return new ResponseTimeEntryBalanceSummaryJson
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Operators = operators
        };
    }

    private ResponseTimeEntryBalanceOperatorJson BuildOperatorSummary(
        Guid operatorId,
        string operatorName,
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<TimeEntryPolicy> policies,
        DateTime branchLocalNow)
    {
        var days = new List<ResponseTimeEntryBalanceSummaryDayJson>(entries.Count);
        var totalHours = 0m;
        var totalBalanceHours = 0m;

        foreach (var entry in entries)
        {
            // Re-run Calculate per row (never read the persisted checkpoint): in-progress
            // current-day rows surface live-recomputed hours, non-Present rows resolve to
            // their abonado/owing constants, and closed rows recompute deterministically.
            // Constants come from the policy row applicable to each entry's own date, so a
            // settings change today never rewrites earlier days in the same window.
            var segments = entry.Segments
                .Where(segment => segment.Active)
                .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
                .ToList();

            var policy = TimeEntryPolicyResolver.Resolve(policies, entry.Date);
            var (dayTotalHours, dayBalanceHours) = calculationService.Calculate(
                entry.Status,
                segments,
                entry.Date,
                branchLocalNow,
                policy.DailyTargetHours,
                policy.LunchDeductionOver6H,
                policy.LunchDeductionOver4H);

            totalHours += dayTotalHours;
            totalBalanceHours += dayBalanceHours;

            days.Add(new ResponseTimeEntryBalanceSummaryDayJson
            {
                Date = entry.Date,
                Status = entry.Status,
                TotalHours = dayTotalHours,
                BalanceHours = dayBalanceHours,
                IsInProgress = entry.Segments.Any(segment => segment.Active && segment.ClockOut is null)
            });
        }

        return new ResponseTimeEntryBalanceOperatorJson
        {
            OperatorId = operatorId,
            OperatorName = operatorName,
            TotalHours = totalHours,
            TotalBalanceHours = totalBalanceHours,
            PresentDays = entries.Count(entry => entry.Status == TimeEntryStatus.Present),
            AbsentDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.JustifiedAbsence or TimeEntryStatus.UnjustifiedAbsence),
            OwingDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.DayOff or TimeEntryStatus.UnjustifiedAbsence),
            AbonadoDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.Sunday or TimeEntryStatus.Holiday
                    or TimeEntryStatus.Vacation or TimeEntryStatus.JustifiedAbsence),
            ContainsInProgress = entries.Any(entry =>
                entry.Segments.Any(segment => segment.Active && segment.ClockOut is null)),
            Days = days
        };
    }

    private static ResponseTimeEntryBalanceSummaryJson EmptyResponse(RequestTimeEntryBalanceSummaryJson request)
    {
        return new ResponseTimeEntryBalanceSummaryJson
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Operators = []
        };
    }

    private static void Validate(RequestTimeEntryBalanceSummaryJson request)
    {
        var result = new TimeEntryBalanceSummaryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
