using System.Globalization;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Settings.LockMonth;

public sealed class LockSettingMonthUseCase(
    IAuthenticationService authenticationService,
    ISettingsRepository settingsRepository,
    IBranchesRepository branchesRepository,
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository,
    MonthLockReadinessEvaluator readinessEvaluator,
    IBranchClock branchClock,
    IMonthLockCoordination monthLockCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseSettingJson> Execute(
        RequestLockSettingMonthJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        Validate(request);
        ct.ThrowIfCancellationRequested();

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not (Role.Manager or Role.Admin))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var requestedMonth = MonthStart(request.Year, request.Month);
        var currentMonth = MonthStart(branchClock.LocalBusinessDate(branchClock.UtcNow()));
        if (requestedMonth >= currentMonth)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_CURRENT_OR_FUTURE);

        await using var coordination = await monthLockCoordination.TryAcquireExclusive(branchUser.BranchId, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);

        var setting = await settingsRepository.GetByBranchId(branchUser.BranchId, ct)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        if (setting.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.SETTING_STALE_WRITE);
        var branch = await branchesRepository.GetById(branchUser.BranchId, ct)
            ?? throw new InvalidOperationException($"Branch row missing for branch {branchUser.BranchId}.");

        var requestedMonthEnd = requestedMonth.AddMonths(1).AddDays(-1);
        // Current/future boundaries could only have been written by the retired PUT path.
        // Rebuild from the data-aware floor so the explicit command can repair that legacy
        // state after validating the requested elapsed interval.
        var hasLegacyCurrentOrFutureBoundary = setting.LockDate.Date >= currentMonth;
        if (hasLegacyCurrentOrFutureBoundary is false && requestedMonthEnd <= setting.LockDate.Date)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_ALREADY_LOCKED);

        var operationalFloor = await ResolveInitialFloor(branchUser.BranchId, branch.CreatedAt, ct);
        // A non-MinValue legacy boundary can also sit near year 1. Clamping its next
        // month to the earliest operational month avoids scanning known-empty centuries.
        var intervalStart = setting.LockDate == DateTime.MinValue || hasLegacyCurrentOrFutureBoundary
            ? operationalFloor
            : LaterMonth(MonthStart(setting.LockDate.Date.AddDays(1)), operationalFloor);

        if (requestedMonth < intervalStart)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_BEFORE_BRANCH_FLOOR);

        for (var month = intervalStart; month <= requestedMonth; month = month.AddMonths(1))
        {
            var readiness = await EvaluateMonth(branchUser.BranchId, month, ct);
            if (readiness.IsReady)
                continue;

            if (month != requestedMonth)
            {
                var blockingMonth = month.ToString("MM/yyyy", CultureInfo.InvariantCulture);
                throw new ConflictException(string.Format(
                    ResourcesErrorMessages.SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED,
                    blockingMonth));
            }

            ThrowRequestedMonthBlocker(readiness);
        }

        setting.LockDate = requestedMonthEnd;
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return setting.ToResponse();
    }

    private async Task<MonthLockReadinessResult> EvaluateMonth(
        Guid branchId,
        DateTime month,
        CancellationToken ct)
    {
        var closes = await dailyClosesRepository.ListByBranchIdAndYearMonthAsNoTracking(
            branchId, month.Year, month.Month, ct);
        var transactionCounts = await transactionsRepository
            .CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                branchId, month.Year, month.Month, ct);
        var activityPairs = await transactionsRepository
            .ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                branchId, month.Year, month.Month, ct);

        return readinessEvaluator.Evaluate(closes, transactionCounts, activityPairs);
    }

    private async Task<DateTime> ResolveInitialFloor(Guid branchId, DateTime branchCreatedAt, CancellationToken ct)
    {
        var floor = MonthStart(branchClock.LocalBusinessDate(branchCreatedAt));

        // CreatedAt is the no-data default, not evidence that operational history cannot exist
        // earlier. Both reads run inside the exclusive month boundary, so a participating writer
        // either commits first and is discovered here, or waits and rechecks the new LockDate.
        var earliestCloseDate = await dailyClosesRepository
            .GetEarliestDateByBranchIdAsNoTracking(branchId, ct);
        var earliestTransactionDate = await transactionsRepository
            .GetEarliestDateByBranchIdAsNoTracking(branchId, ct);

        if (earliestCloseDate is { } closeDate)
            floor = EarlierMonth(floor, closeDate);

        if (earliestTransactionDate is { } transactionDate)
            floor = EarlierMonth(floor, transactionDate);

        return floor;
    }

    private static DateTime EarlierMonth(DateTime currentFloor, DateTime candidateDate)
    {
        var candidateMonth = MonthStart(candidateDate);
        return candidateMonth < currentFloor ? candidateMonth : currentFloor;
    }

    private static DateTime LaterMonth(DateTime candidateStart, DateTime operationalFloor)
    {
        return candidateStart > operationalFloor ? candidateStart : operationalFloor;
    }

    private static void ThrowRequestedMonthBlocker(MonthLockReadinessResult readiness)
    {
        if (readiness.UnapprovedCloses.Count > 0)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);

        if (readiness.HasDraftTransactions)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_DRAFT_TRANSACTIONS);

        if (readiness.HasNoCloseMonthWithExpectedActivity)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_NO_CLOSE_WITH_EXPECTED_ACTIVITY);

        if (readiness.MissingExpectedCloses.Count > 0)
            throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_MISSING_EXPECTED_CLOSE);

        throw new InvalidOperationException("Month-lock readiness was false without an actionable blocker.");
    }

    private static DateTime MonthStart(DateTime date)
    {
        return MonthStart(date.Year, date.Month);
    }

    private static DateTime MonthStart(int year, int month)
    {
        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private static void Validate(RequestLockSettingMonthJson request)
    {
        var result = new LockSettingMonthFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
