using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.TimeEntryBalance;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Reports.TimeEntryBalance;

public class GetTimeEntryBalanceSummaryUseCaseTest
{
    private static readonly DateTime DateFrom = new(2025, 1, 1);
    private static readonly DateTime DateTo = new(2025, 1, 31);
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BranchLocalNow = new(2026, 5, 8, 17, 0, 0);

    private const decimal DailyTarget = 7.33m;

    // -------------------------------------------------------------------------
    // Happy: single operator over a mixed-status period (real calculation service).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldSummarizeMixedStatusPeriod_WhenManagerQueriesOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Operadora Alvo").Build();

        var present = BuildClosedPresentEntry(targetOperator, new DateTime(2026, 5, 2));
        var sunday = BuildStatusOnlyEntry(targetOperator, new DateTime(2026, 5, 3), TimeEntryStatus.Sunday);
        var holiday = BuildStatusOnlyEntry(targetOperator, new DateTime(2026, 5, 4), TimeEntryStatus.Holiday);
        var dayOff = BuildStatusOnlyEntry(targetOperator, new DateTime(2026, 5, 5), TimeEntryStatus.DayOff);

        var ctx = BuildContext(branchUser, calculationService: new TimeEntryCalculationService());
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [present, sunday, holiday, dayOff]);

        var useCase = CreateUseCase(ctx);
        var request = Request().WithOperatorId(targetOperator.Id).Build();

        var response = await useCase.Execute(request);

        response.DateFrom.ShouldBe(DateFrom);
        response.DateTo.ShouldBe(DateTo);
        response.Operators.Count.ShouldBe(1);

        var summary = response.Operators[0];
        summary.OperatorId.ShouldBe(targetOperator.Id);
        summary.OperatorName.ShouldBe("Operadora Alvo");

        // Present: 9 h gross − 1 h lunch = 8 h; abonado: 7.33; owing: 0.
        summary.TotalHours.ShouldBe(8m + DailyTarget + DailyTarget);
        summary.TotalBalanceHours.ShouldBe(0.67m + 0m + 0m - DailyTarget);
        summary.PresentDays.ShouldBe(1);
        summary.AbonadoDays.ShouldBe(2);
        summary.OwingDays.ShouldBe(1);
        summary.AbsentDays.ShouldBe(0);
        summary.ContainsInProgress.ShouldBeFalse();
        summary.Days.Count.ShouldBe(4);

        summary.Days.Single(d => d.Status == TimeEntryStatus.Present).TotalHours.ShouldBe(8m);
        summary.Days.Single(d => d.Status == TimeEntryStatus.Sunday).TotalHours.ShouldBe(DailyTarget);
        summary.Days.Single(d => d.Status == TimeEntryStatus.DayOff).BalanceHours.ShouldBe(-DailyTarget);
    }

    // -------------------------------------------------------------------------
    // Day categorization: UnjustifiedAbsence counts in both Absent and Owing;
    // JustifiedAbsence counts in both Absent and Abonado.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldCountAbsencesInBothViews_WhenStatusesOverlapCategories()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var unjustified = BuildStatusOnlyEntry(targetOperator, new DateTime(2026, 5, 6), TimeEntryStatus.UnjustifiedAbsence);
        var justified = BuildStatusOnlyEntry(targetOperator, new DateTime(2026, 5, 7), TimeEntryStatus.JustifiedAbsence);

        var ctx = BuildContext(branchUser, calculationService: new TimeEntryCalculationService());
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [unjustified, justified]);

        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(targetOperator.Id).Build());

        var summary = response.Operators.Single();
        summary.AbsentDays.ShouldBe(2);
        summary.OwingDays.ShouldBe(1);
        summary.AbonadoDays.ShouldBe(1);
        summary.PresentDays.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Member self via Mine = true — resolves to the linked operator (one element)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberUsesMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubEntries(linkedOperator.Id, DateFrom, DateTo, [BuildClosedPresentEntry(linkedOperator, new DateTime(2025, 1, 10))]);

        var response = await CreateUseCase(ctx).Execute(Request().WithMine(true).Build());

        var summary = response.Operators.Single();
        summary.OperatorId.ShouldBe(linkedOperator.Id);
        summary.OperatorName.ShouldBe(linkedOperator.Name);
        summary.PresentDays.ShouldBe(1);

        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo));
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Member with explicit OperatorId equal to own linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberSuppliesOwnOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubEntries(linkedOperator.Id, DateFrom, DateTo, [BuildClosedPresentEntry(linkedOperator, new DateTime(2025, 1, 12))]);

        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(linkedOperator.Id).Build());

        response.Operators.Single().OperatorId.ShouldBe(linkedOperator.Id);

        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo));
    }

    // -------------------------------------------------------------------------
    // Member omitting both Mine and OperatorId resolves to their own operator —
    // the "neither" request shape that triggers branch-wide for Manager/Admin must
    // never widen a Member's scope.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberOmitsMineAndOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubEntries(linkedOperator.Id, DateFrom, DateTo, [BuildClosedPresentEntry(linkedOperator, new DateTime(2025, 1, 14))]);

        var response = await CreateUseCase(ctx).Execute(Request().Build());

        response.Operators.Single().OperatorId.ShouldBe(linkedOperator.Id);

        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo));
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // 403: Member targeting another operator — no repository call of either kind
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow403_WhenMemberSuppliesAnotherOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator);
        var request = Request().WithOperatorId(Guid.NewGuid()).Build();

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => CreateUseCase(ctx).Execute(request));
        exception.Message.ShouldBe(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);

        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Empty short-circuit: Member with no linked operator — empty Operators, no repo call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptyOperators_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null);
        var response = await CreateUseCase(ctx).Execute(Request().WithMine(true).Build());

        response.DateFrom.ShouldBe(DateFrom);
        response.DateTo.ShouldBe(DateTo);
        response.Operators.ShouldBeEmpty();

        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Empty short-circuit wins even over an explicit other-operator id (not 403)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptyOperators_WhenUnlinkedMemberSuppliesExplicitOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null);
        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(Guid.NewGuid()).Build());

        response.Operators.ShouldBeEmpty();
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        // The unlinked short-circuit must also never fall through to the branch-wide read.
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Manager/Admin queries any branch operator by explicit OperatorId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnSummary_WhenManagerQueriesAnyBranchOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [BuildClosedPresentEntry(targetOperator, new DateTime(2025, 1, 15))]);

        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(targetOperator.Id).Build());

        var summary = response.Operators.Single();
        summary.OperatorId.ShouldBe(targetOperator.Id);
        summary.PresentDays.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // NEW: Manager/Admin omitting both Mine and OperatorId → branch-wide roll-up,
    // one element per operator, grouped + ordered by name (real calculation service).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnAllOperators_WhenManagerOmitsMineAndOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var operatorAlfa = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Alfa").Build();
        var operatorBravo = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Bravo").Build();

        // Branch-wide repo returns a flat list; the use case groups by operator and
        // orders by name. Seed Bravo first to prove the use case reorders.
        var bravoPresent = BuildClosedPresentEntry(operatorBravo, new DateTime(2026, 5, 2));
        var alfaPresent = BuildClosedPresentEntry(operatorAlfa, new DateTime(2026, 5, 2));
        var alfaSunday = BuildStatusOnlyEntry(operatorAlfa, new DateTime(2026, 5, 3), TimeEntryStatus.Sunday);

        var ctx = BuildContext(branchUser, calculationService: new TimeEntryCalculationService());
        ctx.StubBranchWideEntries(DateFrom, DateTo, [bravoPresent, alfaPresent, alfaSunday]);

        var response = await CreateUseCase(ctx).Execute(Request().Build());

        response.Operators.Count.ShouldBe(2);
        response.Operators[0].OperatorName.ShouldBe("Alfa");
        response.Operators[1].OperatorName.ShouldBe("Bravo");

        response.Operators[0].PresentDays.ShouldBe(1);
        response.Operators[0].AbonadoDays.ShouldBe(1);
        response.Operators[0].TotalHours.ShouldBe(8m + DailyTarget);

        response.Operators[1].PresentDays.ShouldBe(1);
        response.Operators[1].TotalHours.ShouldBe(8m);

        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo));
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Branch-wide captures branchLocalNow exactly once for the whole report — every
    // operator's per-row Calculate sees the same timestamp (not re-sampled per operator).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldCaptureBranchLocalNowOnce_AcrossAllOperators()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var operatorAlfa = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Alfa").Build();
        var operatorBravo = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Bravo").Build();

        var entries = new[]
        {
            BuildClosedPresentEntry(operatorAlfa, new DateTime(2026, 5, 2)),
            BuildClosedPresentEntry(operatorAlfa, new DateTime(2026, 5, 3)),
            BuildClosedPresentEntry(operatorBravo, new DateTime(2026, 5, 2))
        };

        var ctx = BuildContext(branchUser);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubBranchWideEntries(DateFrom, DateTo, entries);

        await CreateUseCase(ctx).Execute(Request().Build());

        // Sampled once for the whole report...
        ctx.BranchClock.Received(1).UtcNow();
        ctx.BranchClock.Received(1).LocalBusinessDateTime(FixedUtcNow);

        // ...and every per-row Calculate across both operators received that one timestamp:
        // the count with the exact branchLocalNow equals the total call count.
        ctx.CalculationService.Received(entries.Length).Calculate(
            Arg.Any<TimeEntryStatus>(), Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(), Arg.Any<DateTime>(),
            BranchLocalNow, Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>());
        ctx.CalculationService.Received(entries.Length).Calculate(
            Arg.Any<TimeEntryStatus>(), Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(), Arg.Any<DateTime>(),
            Arg.Any<DateTime>(), Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>());
    }

    // -------------------------------------------------------------------------
    // Branch-wide ordering tie-breaker: equal names fall back to OperatorId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldOrderByOperatorIdOnNameTie_InBranchWideMode()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        // The expected tie-break order is OperatorId ascending (the same default comparer the
        // use case uses). Feed the entries in the REVERSE (descending-Id) order so a missing
        // ThenBy(OperatorId) would leave them descending — making this test actually fail.
        var ascendingIds = new[]
            {
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222")
            }
            .OrderBy(id => id)
            .ToList();
        var descendingIds = Enumerable.Reverse(ascendingIds).ToList();

        var firstFed = new OperatorBuilder().WithId(descendingIds[0]).WithBranchId(branchUser.BranchId).WithName("Tie").Build();
        var secondFed = new OperatorBuilder().WithId(descendingIds[1]).WithBranchId(branchUser.BranchId).WithName("Tie").Build();

        var ctx = BuildContext(branchUser);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubBranchWideEntries(DateFrom, DateTo, [
            BuildClosedPresentEntry(firstFed, new DateTime(2026, 5, 2)),
            BuildClosedPresentEntry(secondFed, new DateTime(2026, 5, 2))
        ]);

        var response = await CreateUseCase(ctx).Execute(Request().Build());

        response.Operators.Count.ShouldBe(2);
        response.Operators.Select(summary => summary.OperatorId).ShouldBe(ascendingIds);
    }

    // -------------------------------------------------------------------------
    // Branch-wide with no entries in the window → empty Operators, no operator load
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptyOperators_WhenBranchWideHasNoEntries()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var ctx = BuildContext(branchUser);
        ctx.StubBranchWideEntries(DateFrom, DateTo, []);

        var response = await CreateUseCase(ctx).Execute(Request().Build());

        response.Operators.ShouldBeEmpty();
        await ctx.OperatorsRepository.DidNotReceive().GetActiveByIdAndBranchIdAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    // -------------------------------------------------------------------------
    // 404: Manager targeting a missing or cross-branch operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow404_WhenOperatorBelongsToAnotherBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var crossBranchOperatorId = Guid.NewGuid();

        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepositoryBuilder.GetActiveByIdAndBranchIdAsNoTracking(crossBranchOperatorId, branchUser.BranchId, null);

        var request = Request().WithOperatorId(crossBranchOperatorId).Build();

        var exception = await Should.ThrowAsync<NotFoundException>(() => CreateUseCase(ctx).Execute(request));
        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // 404: Manager using Mine = true without an own linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow404_WhenManagerUsesMineWithoutLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null);
        var request = Request().WithMine(true).Build();

        var exception = await Should.ThrowAsync<NotFoundException>(() => CreateUseCase(ctx).Execute(request));
        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // Branch isolation: caller's branch id is the only one passed downstream
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPassCallerBranchIdToRepositories()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var otherBranchId = Guid.NewGuid();

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubCalc(8m, 0.67m);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [BuildClosedPresentEntry(targetOperator, new DateTime(2025, 1, 20))]);

        await CreateUseCase(ctx).Execute(Request().WithOperatorId(targetOperator.Id).Build());

        await ctx.OperatorsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == targetOperator.Id),
            Arg.Is<Guid>(v => v == branchUser.BranchId));
        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId),
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // In-progress current-day row recomputes live using the once-captured branchLocalNow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldRecomputeLiveRunning_ForInProgressCurrentDayRow()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var entry = new TimeEntryBuilder()
            .WithOperator(targetOperator)
            .WithDate(BranchLocalNow.Date)
            .WithStatus(TimeEntryStatus.Present)
            .Build();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(BranchLocalNow.Date.AddHours(8))
            .WithClockOut(null)
            .Build());

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubCalc(5m, -2.33m);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [entry]);

        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(targetOperator.Id).Build());

        var summary = response.Operators.Single();
        summary.Days.Count.ShouldBe(1);
        summary.Days[0].TotalHours.ShouldBe(5m);
        summary.Days[0].BalanceHours.ShouldBe(-2.33m);
        summary.Days[0].IsInProgress.ShouldBeTrue();
        summary.TotalHours.ShouldBe(5m);
        summary.TotalBalanceHours.ShouldBe(-2.33m);
        summary.ContainsInProgress.ShouldBeTrue();

        ctx.CalculationService.Received(1).Calculate(
            TimeEntryStatus.Present,
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            BranchLocalNow.Date,
            BranchLocalNow,
            ctx.Setting.DailyTargetHours,
            ctx.Setting.LunchDeductionOver6H,
            ctx.Setting.LunchDeductionOver4H);
    }

    // -------------------------------------------------------------------------
    // Prior-day forgotten-open row: open segment contributes 0, closed sibling counts
    // (real calculation service, Example 6 of §6.7).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldContributeZeroForForgottenOpenSegment_ButCountClosedSibling()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var priorDay = BranchLocalNow.Date.AddDays(-1);
        var entry = new TimeEntryBuilder()
            .WithOperator(targetOperator)
            .WithDate(priorDay)
            .WithStatus(TimeEntryStatus.Present)
            .Build();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry).WithClockIn(priorDay.AddHours(8)).WithClockOut(priorDay.AddHours(12)).Build());
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry).WithClockIn(priorDay.AddHours(13)).WithClockOut(null).Build());

        var ctx = BuildContext(branchUser, calculationService: new TimeEntryCalculationService());
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubEntries(targetOperator.Id, DateFrom, DateTo, [entry]);

        var response = await CreateUseCase(ctx).Execute(Request().WithOperatorId(targetOperator.Id).Build());

        var summary = response.Operators.Single();
        summary.Days.Count.ShouldBe(1);
        summary.Days[0].TotalHours.ShouldBe(4m);
        summary.Days[0].BalanceHours.ShouldBe(4m - DailyTarget);
        summary.Days[0].IsInProgress.ShouldBeTrue();
        summary.ContainsInProgress.ShouldBeTrue();
        summary.TotalHours.ShouldBe(4m);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RequestTimeEntryBalanceSummaryJsonBuilder Request()
    {
        return new RequestTimeEntryBalanceSummaryJsonBuilder()
            .WithMine(false)
            .WithOperatorId(null)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo);
    }

    private static GetTimeEntryBalanceSummaryUseCase CreateUseCase(TestContext ctx)
    {
        return new GetTimeEntryBalanceSummaryUseCase(
            ctx.AuthenticationService,
            ctx.OperatorsRepository,
            ctx.TimeEntriesRepository,
            ctx.SettingsRepository,
            ctx.CalculationService,
            ctx.BranchClock,
            ctx.MemberScopeResolver);
    }

    private static TimeEntry BuildClosedPresentEntry(Operator op, DateTime date)
    {
        var entry = new TimeEntryBuilder()
            .WithOperator(op)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .Build();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(date.AddHours(8))
            .WithClockOut(date.AddHours(17))
            .Build());
        return entry;
    }

    private static TimeEntry BuildStatusOnlyEntry(Operator op, DateTime date, TimeEntryStatus status)
    {
        return new TimeEntryBuilder()
            .WithOperator(op)
            .WithDate(date)
            .WithStatus(status)
            .WithSegments()
            .Build();
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        Operator? linkedOperator = null,
        ITimeEntryCalculationService? calculationService = null)
    {
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            BranchId = branchUser.BranchId,
            DailyTargetHours = DailyTarget,
            LunchDeductionOver6H = 1m,
            LunchDeductionOver4H = 0.25m
        };

        var memberScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(linkedOperator, linkedOperator is null ? [] : [Guid.NewGuid()]));

        var operatorsRepositoryBuilder = new OperatorsRepositoryBuilder();
        var timeEntriesRepositoryBuilder = new TimeEntriesRepositoryBuilder();

        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();

        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(FixedUtcNow).Returns(BranchLocalNow);

        return new TestContext
        {
            BranchUser = branchUser,
            Setting = setting,
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            OperatorsRepositoryBuilder = operatorsRepositoryBuilder,
            OperatorsRepository = operatorsRepositoryBuilder.Build(),
            TimeEntriesRepositoryBuilder = timeEntriesRepositoryBuilder,
            TimeEntriesRepository = timeEntriesRepositoryBuilder.Build(),
            SettingsRepository = settingsRepository,
            CalculationService = calculationService ?? Substitute.For<ITimeEntryCalculationService>(),
            BranchClock = branchClock,
            MemberScopeResolver = memberScopeResolver
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required OperatorsRepositoryBuilder OperatorsRepositoryBuilder { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; init; }
        public required TimeEntriesRepositoryBuilder TimeEntriesRepositoryBuilder { get; init; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required ITimeEntryCalculationService CalculationService { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IMemberAccountScopeResolver MemberScopeResolver { get; init; }

        public void StubOperatorInBranch(Operator targetOperator)
        {
            OperatorsRepositoryBuilder
                .GetActiveByIdAndBranchIdAsNoTracking(targetOperator.Id, BranchUser.BranchId, targetOperator);
        }

        public void StubEntries(Guid operatorId, DateTime dateFrom, DateTime dateTo, IReadOnlyList<TimeEntry> entries)
        {
            TimeEntriesRepositoryBuilder.ListByBranchIdAndOperatorIdAndDateRangeAsNoTrackingReturns(
                BranchUser.BranchId, operatorId, dateFrom, dateTo, entries);
        }

        public void StubBranchWideEntries(DateTime dateFrom, DateTime dateTo, IReadOnlyList<TimeEntry> entries)
        {
            TimeEntriesRepositoryBuilder.ListByBranchIdAndDateRangeAsNoTrackingReturns(
                BranchUser.BranchId, dateFrom, dateTo, entries);
        }

        public void StubCalc(decimal totalHours, decimal balanceHours)
        {
            CalculationService.Calculate(
                    Arg.Any<TimeEntryStatus>(),
                    Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
                    Arg.Any<DateTime>(),
                    Arg.Any<DateTime>(),
                    Arg.Any<decimal>(),
                    Arg.Any<decimal>(),
                    Arg.Any<decimal>())
                .Returns((totalHours, balanceHours));
        }
    }
}
