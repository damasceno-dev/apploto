using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Application.Services.Reports;

/// <summary>
/// Assembles every management-overview section (M11 Phase 7) through the same readers, projections and repository
/// queries as the full reports, one section after another on the request's database context. The caller owns
/// authorization, validation, the single clock read and the read-only snapshot around this call.
/// </summary>
public class ManagementOverviewBuilder(
    ManagerQueueReader managerQueueReader,
    MonthlyReconciliationReader monthlyReconciliationReader,
    UpcomingSettlementsReader upcomingSettlementsReader,
    MonthlyFinancialReader monthlyFinancialReader,
    TimeEntryBalanceProjector timeEntryBalanceProjector,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ITransactionsRepository transactionsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    internal const int RecentClosesDays = 7;
    internal const int RecentClosesPreview = 10;
    internal const int LargestVariancesPreview = 5;
    internal const int TopDebtorsPreview = 5;
    internal const int OpenChequesPreview = 5;
    internal const int UpcomingSettlementsDays = 14;
    internal const int UpcomingSettlementsPreview = 10;
    internal const int ActivityCap = 50;
    internal const int MostBehindPreview = 5;

    public async Task<ResponseManagementOverviewJson> Build(
        Guid branchId,
        DateTime generatedAtUtc,
        DateTime branchLocalNow,
        DateTime asOfDate,
        int year,
        int month,
        CancellationToken ct)
    {
        var (monthFrom, monthTo, isPartialMonth) = MonthlyFinancialReader.MonthWindow(year, month, asOfDate);
        var varianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);

        ct.ThrowIfCancellationRequested();
        var queue = await BuildQueue(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var recentCloses = await BuildRecentCloses(branchId, asOfDate, varianceProductId, ct);
        ct.ThrowIfCancellationRequested();
        var cashVariance = await BuildCashVariance(branchId, monthFrom, monthTo, varianceProductId, ct);
        ct.ThrowIfCancellationRequested();
        var reconciliation = await BuildReconciliation(branchId, year, month, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var fiado = await BuildFiado(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var openCheques = await BuildOpenCheques(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var upcomingSettlements = await BuildUpcomingSettlements(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var accountActivity = await BuildAccountActivity(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var operatorActivity = await BuildOperatorActivity(branchId, asOfDate, ct);
        ct.ThrowIfCancellationRequested();
        var hoursRollUp = await timeEntryBalanceProjector.BuildBranchWide(branchId, monthFrom, monthTo, branchLocalNow, ct);
        var hours = BuildHours(hoursRollUp, monthFrom, monthTo, asOfDate);
        ct.ThrowIfCancellationRequested();
        var monthlyFinancial = await BuildMonthlyFinancial(branchId, year, month, asOfDate, hoursRollUp, ct);
        ct.ThrowIfCancellationRequested();

        return new ResponseManagementOverviewJson
        {
            GeneratedAtUtc = generatedAtUtc,
            AsOfDate = asOfDate,
            BranchLocalNow = branchLocalNow,
            Year = year,
            Month = month,
            MonthDateFrom = monthFrom,
            MonthDateTo = monthTo,
            IsPartialMonth = isPartialMonth,
            Queue = queue,
            RecentCloses = recentCloses,
            CashVariance = cashVariance,
            Reconciliation = reconciliation,
            Fiado = fiado,
            OpenCheques = openCheques,
            UpcomingSettlements = upcomingSettlements,
            AccountActivity = accountActivity,
            OperatorActivity = operatorActivity,
            Hours = hours,
            MonthlyFinancial = monthlyFinancial
        };
    }

    private async Task<ResponseManagementOverviewQueueJson> BuildQueue(Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        var queue = await managerQueueReader.Read(branchId, asOfDate, asOfDate, ct);
        var rejectedCount = queue.Closes.Count(close => close.Status == DailyCloseStatus.Rejected);
        var nothingPending = queue.PendingApprovalCount == 0 && rejectedCount == 0 && queue.NotSubmitted.Count == 0 &&
            queue.Backlog.SubmittedCount == 0 && queue.Backlog.RejectedCount == 0 && queue.Backlog.DraftCount == 0 &&
            queue.UnlockedDraftTransactionCount == 0 && queue.PendingOperatorLinkRequestCount == 0;

        return new ResponseManagementOverviewQueueJson
        {
            Status = nothingPending ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            Date = queue.Date,
            PendingApprovalCount = queue.PendingApprovalCount,
            RejectedCount = rejectedCount,
            NotSubmittedCount = queue.NotSubmitted.Count,
            BacklogSubmittedCount = queue.Backlog.SubmittedCount,
            BacklogRejectedCount = queue.Backlog.RejectedCount,
            BacklogDraftCount = queue.Backlog.DraftCount,
            OldestPendingDate = queue.Backlog.OldestPendingDate,
            UnlockedDraftTransactionCount = queue.UnlockedDraftTransactionCount,
            PendingOperatorLinkRequestCount = queue.PendingOperatorLinkRequestCount
        };
    }

    private async Task<ResponseManagementOverviewRecentClosesJson> BuildRecentCloses(
        Guid branchId, DateTime asOfDate, Guid varianceProductId, CancellationToken ct)
    {
        var dateFrom = asOfDate.AddDays(-(RecentClosesDays - 1));
        var counts = await dailyClosesRepository.CountByStatusByBranchIdAndDateRangeAsNoTracking(branchId, dateFrom, asOfDate, ct);
        var preview = await dailyClosesRepository.ListCloseSummariesByBranchIdAndDateRangeAsNoTracking(
            branchId, dateFrom, asOfDate, RecentClosesPreview, ct);
        // Official values only: a Draft close has no row here, so a retained (recalled or recheck-demoted) value is null.
        var officialByClose = (await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                branchId, varianceProductId, accountId: null, dateFrom, asOfDate, ct))
            .ToDictionary(row => row.DailyCloseId, row => row.Value);

        int CountOf(DailyCloseStatus status) => counts.Where(row => row.Status == status).Sum(row => row.Count);

        var totalCount = counts.Sum(row => row.Count);
        return new ResponseManagementOverviewRecentClosesJson
        {
            Status = totalCount == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            DateFrom = dateFrom,
            DateTo = asOfDate,
            TotalCount = totalCount,
            DraftCount = CountOf(DailyCloseStatus.Draft),
            SubmittedCount = CountOf(DailyCloseStatus.Submitted),
            ApprovedCount = CountOf(DailyCloseStatus.Approved),
            RejectedCount = CountOf(DailyCloseStatus.Rejected),
            PreviewLimit = RecentClosesPreview,
            Items = preview
                .Select(close => new ResponseManagementOverviewCloseJson
                {
                    DailyCloseId = close.DailyCloseId,
                    Date = close.Date,
                    AccountId = close.AccountId,
                    AccountName = close.AccountName,
                    AccountActive = close.AccountActive,
                    Status = close.Status,
                    OfficialVariance = officialByClose.TryGetValue(close.DailyCloseId, out var value) ? value : null,
                    RecordedByUserId = close.RecordedByUserId,
                    RecordedByUserName = close.RecordedByUserName,
                    RecordedByOperatorId = close.RecordedByOperatorId,
                    RecordedByOperatorName = close.RecordedByOperatorName,
                    SubmittedByUserId = close.SubmittedByUserId,
                    SubmittedByUserName = close.SubmittedByUserName,
                    SubmittedByOperatorId = close.SubmittedByOperatorId,
                    SubmittedByOperatorName = close.SubmittedByOperatorName
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewCashVarianceJson> BuildCashVariance(
        Guid branchId, DateTime monthFrom, DateTime monthTo, Guid varianceProductId, CancellationToken ct)
    {
        // Totals folded per status in the database and the preview limited there: no month of rows is materialized.
        var figures = CashVarianceFold.FromStatusTotals(
            await dailyCloseItemsRepository.ListOfficialVarianceStatusTotalsAsNoTracking(
                branchId, varianceProductId, accountId: null, monthFrom, monthTo, ct));
        var largest = await dailyCloseItemsRepository.ListLargestOfficialVariancesAsNoTracking(
            branchId, varianceProductId, monthFrom, monthTo, LargestVariancesPreview, ct);

        return new ResponseManagementOverviewCashVarianceJson
        {
            Status = figures.Count == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            DateFrom = monthFrom,
            DateTo = monthTo,
            TotalCount = figures.Count,
            TotalVariance = figures.Total,
            MeanVariance = figures.Mean,
            MaxVariance = figures.Max,
            MinVariance = figures.Min,
            SubmittedCount = figures.SubmittedCount,
            ApprovedCount = figures.ApprovedCount,
            RejectedCount = figures.RejectedCount,
            PreviewLimit = LargestVariancesPreview,
            LargestVariances = largest
                .Select(row => new ResponseCashVarianceSummaryItemJson
                {
                    DailyCloseId = row.DailyCloseId,
                    Date = row.Date,
                    AccountId = row.AccountId,
                    AccountName = row.AccountName,
                    VarianceValue = row.Value,
                    DailyCloseStatus = row.DailyCloseStatus
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewReconciliationJson> BuildReconciliation(
        Guid branchId, int year, int month, DateTime asOfDate, CancellationToken ct)
    {
        // The lock evaluator's own inputs only; the full report's per-day variance and detail are not read.
        var context = await monthlyReconciliationReader.ReadLockContext(branchId, year, month, asOfDate, ct);
        var dateFrom = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return new ResponseManagementOverviewReconciliationJson
        {
            Status = context.HasRecordedActivity ? OverviewSectionStatus.Ready : OverviewSectionStatus.Empty,
            Year = year,
            Month = month,
            DateFrom = dateFrom,
            DateTo = dateFrom.AddMonths(1).AddDays(-1),
            LockDate = context.LockDate,
            IsMonthLocked = context.IsMonthLocked,
            IsMonthElapsed = context.IsMonthElapsed,
            LockReady = context.LockReady,
            UnapprovedCloseBlockerCount = context.UnapprovedCloseBlockerCount,
            DraftTransactionBlockerCount = context.DraftTransactionBlockerCount,
            MissingExpectedCloseBlockerCount = context.MissingExpectedCloseBlockerCount
        };
    }

    private async Task<ResponseManagementOverviewFiadoJson> BuildFiado(Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        // The split and the top debtors come from the shared balances projection, folded and limited in the database.
        var summary = await transactionsRepository.GetFiadoBalanceSummaryByBranchIdAsNoTracking(branchId, asOfDate, ct);
        var topDebtors = await transactionsRepository.ListTopFiadoDebtorsByBranchIdAsNoTracking(
            branchId, asOfDate, TopDebtorsPreview, ct);
        var aging = await transactionsRepository.GetFiadoAgingTotalsByBranchIdAsNoTracking(branchId, null, null, asOfDate, ct);
        var totalCount = summary.DebtorCount + summary.CreditorCount;

        return new ResponseManagementOverviewFiadoJson
        {
            Status = totalCount == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            AsOfDate = asOfDate,
            TotalCount = totalCount,
            DebtorCount = summary.DebtorCount,
            TotalDebt = summary.TotalDebt,
            CreditorCount = summary.CreditorCount,
            TotalCredit = summary.TotalCredit,
            AgingOutstandingTotal = aging.ByDueDate.Sum(row => row.Value),
            AgingByBucket = AgingBucketTotals.Build(aging.ByDueDate, asOfDate),
            OldestUnpaidDate = aging.OldestUnpaidDate,
            PreviewLimit = TopDebtorsPreview,
            TopDebtors = topDebtors
                .Select(row => new ResponseFiadoClientBalanceItemJson
                {
                    ClientId = row.ClientId,
                    ClientName = row.ClientName,
                    OutstandingTotal = row.OutstandingTotal
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewOpenChequesJson> BuildOpenCheques(Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        var totalCount = await transactionsRepository.CountOpenChequeGroupsByBranchIdAsNoTracking(branchId, null, null, ct);
        var dueDateTotals = await transactionsRepository.ListOpenChequeTotalsByDueDateAsNoTracking(branchId, null, null, ct);
        var groups = await transactionsRepository.ListOpenChequeGroupsByBranchIdAsNoTracking(
            branchId, null, null, 1, OpenChequesPreview, ct);

        return new ResponseManagementOverviewOpenChequesJson
        {
            Status = totalCount == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            AsOfDate = asOfDate,
            TotalCount = totalCount,
            OpenRowCount = dueDateTotals.Sum(row => row.RowCount),
            OutstandingTotal = dueDateTotals.Sum(row => row.Value),
            ByBucket = AgingBucketTotals.Build(dueDateTotals, asOfDate),
            OldestOpenDueDate = dueDateTotals.Count == 0 ? null : dueDateTotals.Min(row => row.DueDate),
            PreviewLimit = OpenChequesPreview,
            Items = groups
                .Where(group => group.OpenRows.Count > 0)
                .Select(group => new ResponseManagementOverviewOpenChequeJson
                {
                    OriginTransactionId = group.OriginTransactionId,
                    OldestOpenTransactionId = group.OpenRows[0].TransactionId,
                    OutstandingTotal = group.OutstandingTotal,
                    OldestOpenDueDate = group.OldestOpenDueDate,
                    OldestOpenBucket = ReportAgingBucketizer.BucketFor(group.OldestOpenDueDate, asOfDate),
                    OpenRowCount = group.OpenRowCount,
                    TotalRowCount = group.TotalRowCount,
                    AccountId = group.AccountId,
                    AccountName = group.AccountName,
                    ClientId = group.ClientId,
                    ClientName = group.ClientName
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewUpcomingSettlementsJson> BuildUpcomingSettlements(
        Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        var report = await upcomingSettlementsReader.Read(
            branchId, asOfDate, asOfDate, asOfDate.AddDays(UpcomingSettlementsDays - 1), null, null, 1, UpcomingSettlementsPreview, ct);

        var status = report.IsConfigured is false ? OverviewSectionStatus.NotConfigured
            : report.TotalCount == 0 && report.OverdueCount == 0 ? OverviewSectionStatus.Empty
            : OverviewSectionStatus.Ready;

        return new ResponseManagementOverviewUpcomingSettlementsJson
        {
            Status = status,
            AsOfDate = report.AsOfDate,
            DueDateFrom = report.DueDateFrom,
            DueDateTo = report.DueDateTo,
            TotalCount = report.TotalCount,
            TotalValue = report.TotalValue,
            ByInstrument = report.ByInstrument,
            OverdueCount = report.OverdueCount,
            OverdueValue = report.OverdueValue,
            PreviewLimit = UpcomingSettlementsPreview,
            Items = report.Items
        };
    }

    private async Task<ResponseManagementOverviewAccountActivityJson> BuildAccountActivity(
        Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        // Every active Terminal plus any account, active or deactivated, with activity today — counted and capped in the
        // database; sums and last-activity dates are read only for the listed page.
        var page = await transactionsRepository.GetAccountActivityPageByBranchIdAndDateAsNoTracking(
            branchId, asOfDate, ActivityCap, ct);
        var lastActivity = (await transactionsRepository.ListLastActivityDatesByBranchIdAsNoTracking(
                branchId, page.Items.Select(row => row.AccountId).ToList(), asOfDate, ct))
            .ToDictionary(row => row.AccountId, row => row.LastActivityDate);

        return new ResponseManagementOverviewAccountActivityJson
        {
            Status = page.TotalCount == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            Date = asOfDate,
            TotalCount = page.TotalCount,
            PreviewLimit = ActivityCap,
            Items = page.Items
                .Select(row => new ResponseManagementOverviewAccountJson
                {
                    AccountId = row.AccountId,
                    AccountName = row.AccountName,
                    AccountType = row.AccountType,
                    IsActive = row.AccountActive,
                    TotalIn = row.TotalIn,
                    TotalOut = row.TotalOut,
                    Net = row.TotalIn - row.TotalOut,
                    ActiveCount = row.ActiveCount,
                    DraftCount = row.DraftCount,
                    LastActivityDate = lastActivity.TryGetValue(row.AccountId, out var last) ? last : null
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewOperatorActivityJson> BuildOperatorActivity(
        Guid branchId, DateTime asOfDate, CancellationToken ct)
    {
        // Active operators plus any operator with rows today — counted, ordered and capped in the database.
        var page = await transactionsRepository.GetOperatorActivityPageByBranchIdAndDateRangeAsNoTracking(
            branchId, asOfDate, asOfDate, ActivityCap, ct);

        return new ResponseManagementOverviewOperatorActivityJson
        {
            Status = page.TotalCount == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            Date = asOfDate,
            TotalCount = page.TotalCount,
            PreviewLimit = ActivityCap,
            Items = page.Items
                .Select(row => new ResponseManagementOverviewOperatorJson
                {
                    OperatorId = row.OperatorId,
                    OperatorName = row.OperatorName,
                    IsActive = row.OperatorActive,
                    TransactionCount = row.TransactionCount,
                    TotalIn = row.TotalIn,
                    TotalOut = row.TotalOut,
                    GeneratedCount = row.GeneratedCount,
                    GeneratedIn = row.GeneratedIn,
                    GeneratedOut = row.GeneratedOut
                })
                .ToList()
        };
    }

    // The time-entry calculation runs in C# (each day's effective policy, one branch-local now), so the month's roll-up is
    // necessarily computed whole; it is read once and shared with the monthly financial section.
    private static ResponseManagementOverviewHoursJson BuildHours(
        IReadOnlyList<ResponseTimeEntryBalanceOperatorJson> operators, DateTime monthFrom, DateTime monthTo, DateTime asOfDate)
    {
        var positive = operators.Where(op => op.TotalBalanceHours > 0m).ToList();
        var negative = operators.Where(op => op.TotalBalanceHours < 0m).ToList();

        return new ResponseManagementOverviewHoursJson
        {
            Status = operators.Count == 0 ? OverviewSectionStatus.Empty : OverviewSectionStatus.Ready,
            DateFrom = monthFrom,
            DateTo = monthTo,
            TotalCount = operators.Count,
            LiveNowCount = operators.Count(op => op.Days.Any(day => day.IsInProgress && day.Date.Date == asOfDate.Date)),
            UnresolvedOpenCount = operators.Sum(op => op.Days.Count(day => day.IsInProgress && day.Date.Date < asOfDate.Date)),
            ContainsInProgress = operators.Any(op => op.ContainsInProgress),
            PositiveBalanceCount = positive.Count,
            PositiveBalanceHours = positive.Sum(op => op.TotalBalanceHours),
            NegativeBalanceCount = negative.Count,
            NegativeBalanceHours = negative.Sum(op => op.TotalBalanceHours),
            PreviewLimit = MostBehindPreview,
            MostBehind = operators
                .OrderBy(op => op.TotalBalanceHours)
                .ThenBy(op => op.OperatorName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(op => op.OperatorId)
                .Take(MostBehindPreview)
                .Select(op => new ResponseManagementOverviewHoursOperatorJson
                {
                    OperatorId = op.OperatorId,
                    OperatorName = op.OperatorName,
                    IsActive = op.OperatorActive,
                    TotalHours = op.TotalHours,
                    TotalBalanceHours = op.TotalBalanceHours,
                    ContainsInProgress = op.ContainsInProgress
                })
                .ToList()
        };
    }

    private async Task<ResponseManagementOverviewMonthlyFinancialJson> BuildMonthlyFinancial(
        Guid branchId,
        int year,
        int month,
        DateTime asOfDate,
        IReadOnlyList<ResponseTimeEntryBalanceOperatorJson> hoursRollUp,
        CancellationToken ct)
    {
        // Totals only, folded in the database from the full report's own predicates; no day, leftover or close list.
        var totals = await monthlyFinancialReader.ReadTotals(branchId, year, month, asOfDate, hoursRollUp, ct);
        var status = totals.IsBolaoServiceFeeConfigured is false && totals.IsCommercialLeftoverConfigured is false
            ? OverviewSectionStatus.NotConfigured
            : totals.CloseCount == 0 && totals.BolaoServiceFeeRowCount == 0 && totals.CommercialLeftoverRowCount == 0 &&
              totals.HourDayCount == 0
                ? OverviewSectionStatus.Empty
                : OverviewSectionStatus.Ready;

        return new ResponseManagementOverviewMonthlyFinancialJson
        {
            Status = status,
            Year = year,
            Month = month,
            DateFrom = totals.DateFrom,
            DateTo = totals.DateTo,
            IsPartialMonth = totals.IsPartialMonth,
            OfficialVarianceTotal = totals.OfficialVarianceTotal,
            OfficialCloseCount = totals.OfficialCloseCount,
            IsBolaoServiceFeeConfigured = totals.IsBolaoServiceFeeConfigured,
            BolaoServiceFeeTotal = totals.BolaoServiceFeeTotal,
            IsCommercialLeftoverConfigured = totals.IsCommercialLeftoverConfigured,
            CommercialLeftoverTotal = totals.CommercialLeftoverTotal,
            HoursTotal = totals.HoursTotal,
            ContainsInProgress = totals.ContainsInProgress
        };
    }
}
