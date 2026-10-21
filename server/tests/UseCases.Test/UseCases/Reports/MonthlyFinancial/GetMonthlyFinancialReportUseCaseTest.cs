using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Reports;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.MonthlyFinancial;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.MonthlyFinancial;

public class GetMonthlyFinancialReportUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BranchLocalNow = new(2026, 10, 20, 12, 0, 0);
    private static readonly DateTime AsOfDate = new(2026, 10, 20);

    [Fact]
    public async Task Execute_ShouldFoldOfficialVarianceFeeAndLeftovers_PerTerminalDay_WithoutAGrandTotal()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 9, 1);
        var monthEnd = new DateTime(2026, 9, 30);
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var approvedClose = Summary(new DateTime(2026, 9, 2), t1, "T1", DailyCloseStatus.Approved);
        var draftClose = Summary(new DateTime(2026, 9, 3), t1, "T1", DailyCloseStatus.Draft);
        var opA = Guid.NewGuid();
        var feeType = Guid.NewGuid();
        var bolaoLeftover = Guid.NewGuid();
        var federalLeftover = Guid.NewGuid();
        var ctx = Context(branchUser,
            types => types.ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(branchUser.BranchId,
                [MonthlyFinancialRole.BolaoServiceFee, MonthlyFinancialRole.CommercialLeftover]),
            closes => closes
                .ListCloseSummariesByBranchIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, monthStart, monthEnd, null,
                    [draftClose, approvedClose])
                .CountByStatusByBranchIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, monthStart, monthEnd,
                    [new DailyCloseStatusCountRow(DailyCloseStatus.Approved, 1), new DailyCloseStatusCountRow(DailyCloseStatus.Draft, 1)]),
            items => items
                .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, productId, null,
                    monthStart, monthEnd,
                    [new VarianceTimeSeriesRow(approvedClose.Date, t1, "T1", -12.5m, DailyCloseStatus.Approved, approvedClose.DailyCloseId)])
                .ListOfficialVarianceStatusTotalsAsNoTrackingReturns(branchUser.BranchId, productId, null, monthStart, monthEnd,
                    [new OfficialVarianceStatusTotalRow(DailyCloseStatus.Approved, 1, -12.5m, -12.5m, -12.5m)]),
            transactions => transactions
                .GetMonthlyFinancialRoleTotalAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.BolaoServiceFee,
                    monthStart, monthEnd, new MonthlyFinancialRoleTotal(210m, 20m, 5))
                .GetMonthlyFinancialRoleTotalAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.CommercialLeftover,
                    monthStart, monthEnd, new MonthlyFinancialRoleTotal(5m, 40m, 4))
                .ListMonthlyFinancialRoleRowsByBranchIdAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.BolaoServiceFee,
                    monthStart, monthEnd,
                    [
                        RoleRow(approvedClose.Date, t1, "T1", opA, feeType, "Tarifa Bolão", 140m, 20m, 3),
                        RoleRow(approvedClose.Date, t1, "T1", Guid.NewGuid(), feeType, "Tarifa Bolão", 30m, 0m, 1),
                        RoleRow(new DateTime(2026, 9, 5), t2, "T2", opA, feeType, "Tarifa Bolão", 40m, 0m, 1)
                    ])
                .ListMonthlyFinancialRoleRowsByBranchIdAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.CommercialLeftover,
                    monthStart, monthEnd,
                    [
                        RoleRow(approvedClose.Date, t1, "T1", opA, bolaoLeftover, "Sobra de Bolão", 5m, 25m, 2),
                        RoleRow(new DateTime(2026, 9, 4), t1, "T1", opA, bolaoLeftover, "Sobra de Bolão", 0m, 5m, 1),
                        RoleRow(approvedClose.Date, t1, "T1", opA, federalLeftover, "Sobra de Federal", 0m, 10m, 1)
                    ]),
            productId);

        var response = await ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 9 });

        response.DateFrom.ShouldBe(monthStart);
        response.DateTo.ShouldBe(monthEnd);
        response.IsPartialMonth.ShouldBeFalse();
        response.OfficialVarianceTotal.ShouldBe(-12.5m);
        response.OfficialCloseCount.ShouldBe(1);
        response.BolaoServiceFeeTotal.ShouldBe(190m);
        // Signed per source scope: fee = In − Out, leftover = Out − In (a loss shown positive).
        response.CommercialLeftoverTotal.ShouldBe(35m);
        response.LeftoversByType.Select(total => (total.TransactionTypeName, total.Total, total.RowCount))
            .ShouldBe([("Sobra de Bolão", 25m, 3), ("Sobra de Federal", 10m, 1)]);

        // One row per close (fee summed across the two recording operators, never split) plus a fee-only day.
        response.TerminalDays.Count.ShouldBe(3);
        var approvedDay = response.TerminalDays[0];
        approvedDay.DailyCloseId.ShouldBe(approvedClose.DailyCloseId);
        approvedDay.OfficialVariance.ShouldBe(-12.5m);
        approvedDay.BolaoServiceFee.ShouldBe(150m);
        approvedDay.BolaoServiceFeeRowCount.ShouldBe(4);
        var draftDay = response.TerminalDays[1];
        draftDay.CloseStatus.ShouldBe(DailyCloseStatus.Draft);
        draftDay.OfficialVariance.ShouldBeNull();
        draftDay.BolaoServiceFee.ShouldBe(0m);
        var feeOnlyDay = response.TerminalDays[2];
        feeOnlyDay.DailyCloseId.ShouldBeNull();
        feeOnlyDay.AccountId.ShouldBe(t2);
        feeOnlyDay.BolaoServiceFee.ShouldBe(40m);
        response.Leftovers.Select(row => (row.Date.Day, row.Value)).ShouldBe([(2, 20m), (2, 10m), (4, 5m)]);
    }

    [Fact]
    public async Task Execute_ShouldReportUnconfiguredFiguresAsNull_AndSkipTheirQueries()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var productId = Guid.NewGuid();
        var close = Summary(new DateTime(2026, 10, 5), Guid.NewGuid(), "T1", DailyCloseStatus.Approved);
        var ctx = Context(branchUser,
            types => types.ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(branchUser.BranchId, []),
            closes => closes.ListCloseSummariesByBranchIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId,
                new DateTime(2026, 10, 1), AsOfDate, null, [close]),
            items => items,
            transactions => transactions,
            productId);

        var response = await ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 10 });

        // A close day still lists, but its fee is "not configured", never zero.
        response.TerminalDays.Single().BolaoServiceFee.ShouldBeNull();

        response.IsPartialMonth.ShouldBeTrue();
        response.DateTo.ShouldBe(AsOfDate);
        response.BranchLocalNow.ShouldBe(BranchLocalNow);
        response.IsBolaoServiceFeeConfigured.ShouldBeFalse();
        response.BolaoServiceFeeTotal.ShouldBeNull();
        response.IsCommercialLeftoverConfigured.ShouldBeFalse();
        response.CommercialLeftoverTotal.ShouldBeNull();
        await ctx.Transactions.DidNotReceiveWithAnyArgs().ListMonthlyFinancialRoleRowsByBranchIdAsNoTracking(
            default, default, default, default, default);
        await ctx.Transactions.DidNotReceiveWithAnyArgs().GetMonthlyFinancialRoleTotalAsNoTracking(
            default, default, default, default, default);
    }

    [Fact]
    public async Task Execute_ShouldRefuseAMonthThatHasNotStarted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = Context(branchUser, types => types, closes => closes, items => items, transactions => transactions, Guid.NewGuid());

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 11 }));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
    }

    [Fact]
    public async Task ReadTotals_ShouldCarryRowCountsAndHours_FromTheAggregates()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 9, 1);
        var monthEnd = new DateTime(2026, 9, 30);
        var ctx = Context(branchUser,
            types => types.ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(branchUser.BranchId,
                [MonthlyFinancialRole.BolaoServiceFee, MonthlyFinancialRole.CommercialLeftover]),
            closes => closes.CountByStatusByBranchIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, monthStart, monthEnd,
                [new DailyCloseStatusCountRow(DailyCloseStatus.Draft, 2), new DailyCloseStatusCountRow(DailyCloseStatus.Approved, 1)]),
            items => items.ListOfficialVarianceStatusTotalsAsNoTrackingReturns(branchUser.BranchId, productId, null, monthStart, monthEnd,
                [new OfficialVarianceStatusTotalRow(DailyCloseStatus.Approved, 1, 4m, 4m, 4m)]),
            transactions => transactions
                .GetMonthlyFinancialRoleTotalAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.BolaoServiceFee,
                    monthStart, monthEnd, new MonthlyFinancialRoleTotal(100m, 10m, 5))
                .GetMonthlyFinancialRoleTotalAsNoTrackingReturns(branchUser.BranchId, MonthlyFinancialRole.CommercialLeftover,
                    monthStart, monthEnd, new MonthlyFinancialRoleTotal(2m, 30m, 4)),
            productId);
        server.Communication.Responses.ResponseTimeEntryBalanceOperatorJson Op(decimal hours, bool inProgress, int days) => new()
        {
            OperatorId = Guid.NewGuid(),
            OperatorName = "Op",
            TotalHours = hours,
            ContainsInProgress = inProgress,
            Days = Enumerable.Range(1, days)
                .Select(day => new server.Communication.Responses.ResponseTimeEntryBalanceSummaryDayJson { Date = new DateTime(2026, 9, day) })
                .ToList()
        };

        var totals = await ctx.Reader.ReadTotals(branchUser.BranchId, 2026, 9, AsOfDate, [Op(8m, true, 2), Op(4m, false, 1)]);

        totals.ShouldBe(new MonthlyFinancialTotals(monthStart, monthEnd, false, 4m, 1, 3, true, 90m, 5, true, 28m, 4, 12m, 3, true));
    }

    [Fact]
    public async Task Read_ShouldOrderHoursByDateThenOperatorName()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = Context(branchUser, types => types, closes => closes, items => items, transactions => transactions, Guid.NewGuid());
        server.Communication.Responses.ResponseTimeEntryBalanceOperatorJson Op(string name, params int[] days) => new()
        {
            OperatorId = Guid.NewGuid(),
            OperatorName = name,
            OperatorActive = true,
            Days = days.Select(day => new server.Communication.Responses.ResponseTimeEntryBalanceSummaryDayJson
            {
                Date = new DateTime(2026, 9, day), TotalHours = day, Status = TimeEntryStatus.Present
            }).ToList()
        };

        var report = await ctx.Reader.Read(branchUser.BranchId, 2026, 9, AsOfDate, BranchLocalNow, default,
            [Op("Bruno", 3, 1), Op("ana", 3, 2)]);

        report.Hours.Select(day => (day.Date.Day, day.OperatorName, day.TotalHours))
            .ShouldBe([(1, "Bruno", 1m), (2, "ana", 2m), (3, "ana", 3m), (3, "Bruno", 3m)]);
    }

    [Theory]
    [InlineData(31, true)]   // the month's last day: still in progress, so partial
    [InlineData(30, true)]
    public void MonthWindow_ShouldFlagTheCurrentMonthPartial_ThroughItsLastDay(int today, bool partial)
    {
        var (dateFrom, dateTo, isPartial) = MonthlyFinancialReader.MonthWindow(2026, 10, new DateTime(2026, 10, today));

        dateFrom.ShouldBe(new DateTime(2026, 10, 1));
        dateTo.ShouldBe(new DateTime(2026, 10, today));
        isPartial.ShouldBe(partial);
    }

    [Fact]
    public void MonthWindow_ShouldCoverAnElapsedMonthWhole_AndFutureMonthStartsAfterToday()
    {
        MonthlyFinancialReader.MonthWindow(2026, 9, new DateTime(2026, 10, 1))
            .ShouldBe((new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), false));
        MonthlyFinancialReader.IsFutureMonth(2026, 10, new DateTime(2026, 10, 1)).ShouldBeFalse(); // first day: current
        MonthlyFinancialReader.IsFutureMonth(2026, 11, new DateTime(2026, 10, 31)).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldFailValidation_ForAnInvalidMonth()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = Context(branchUser, types => types, closes => closes, items => items, transactions => transactions, Guid.NewGuid());

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 13 }));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.REPORT_MONTH_INVALID]);
    }

    [Fact]
    public async Task Execute_ShouldNotStart_WhenTheRequestIsAlreadyCancelled()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = Context(branchUser, types => types, closes => closes, items => items, transactions => transactions, Guid.NewGuid());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 9 }, cancelled.Token));
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var ctx = Context(branchUser, types => types, closes => closes, items => items, transactions => transactions, Guid.NewGuid());

        await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            ctx.UseCase.Execute(new RequestMonthlyFinancialReportJson { Year = 2026, Month = 9 }));
    }

    private static DailyCloseSummaryRow Summary(DateTime date, Guid accountId, string accountName, DailyCloseStatus status) =>
        new(Guid.NewGuid(), date, date, accountId, accountName, true, status, null, null, null, null, null, null, null, null);

    private static MonthlyFinancialRoleRow RoleRow(DateTime date, Guid accountId, string accountName, Guid operatorId, Guid typeId,
        string typeName, decimal inTotal, decimal outTotal, int rowCount) =>
        new(date, accountId, accountName, true, operatorId, "Operator", true, typeId, typeName, true, inTotal, outTotal, rowCount);

    private sealed record TestContext(
        GetMonthlyFinancialReportUseCase UseCase,
        server.Domain.Interfaces.ITransactionsRepository Transactions,
        MonthlyFinancialReader Reader);

    private static TestContext Context(
        BranchUser branchUser,
        Func<TransactionTypesRepositoryBuilder, TransactionTypesRepositoryBuilder> types,
        Func<DailyClosesRepositoryBuilder, DailyClosesRepositoryBuilder> closes,
        Func<DailyCloseItemsRepositoryBuilder, DailyCloseItemsRepositoryBuilder> items,
        Func<TransactionsRepositoryBuilder, TransactionsRepositoryBuilder> transactions,
        Guid productId)
    {
        var clock = Substitute.For<IBranchClock>();
        clock.UtcNow().Returns(FixedUtcNow);
        clock.LocalBusinessDateTime(FixedUtcNow).Returns(BranchLocalNow);
        clock.LocalBusinessDate(FixedUtcNow).Returns(AsOfDate);
        var transactionsRepository = transactions(new TransactionsRepositoryBuilder()).Build();
        var reader = new MonthlyFinancialReader(
            closes(new DailyClosesRepositoryBuilder()).Build(),
            items(new DailyCloseItemsRepositoryBuilder()).Build(),
            transactionsRepository,
            types(new TransactionTypesRepositoryBuilder()).Build(),
            new CashVarianceProductResolverBuilder().ReturnsId(branchUser.BranchId, productId).Build(),
            new TimeEntryBalanceProjector(
                new OperatorsRepositoryBuilder().ListActiveByBranchId(branchUser.BranchId, []).Build(),
                new TimeEntriesRepositoryBuilder().Build(),
                new TimeEntryPoliciesRepositoryBuilder().ListActiveByBranchIdAsNoTrackingReturns(branchUser.BranchId,
                    [new TimeEntryPolicyBuilder().WithBranchId(branchUser.BranchId).Build()]).Build(),
                new TimeEntryCalculationService()));
        var useCase = new GetMonthlyFinancialReportUseCase(
            new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build(), reader, clock);
        return new TestContext(useCase, transactionsRepository, reader);
    }
}
