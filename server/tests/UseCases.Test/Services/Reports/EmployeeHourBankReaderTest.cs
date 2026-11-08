using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Reports;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;
namespace UseCases.Test.Services.Reports;

public class EmployeeHourBankReaderTest
{
    private static readonly DateTime Sep = new(2026, 9, 1);
    private static readonly DateTime Oct = new(2026, 10, 1);
    private static Task<ResponseHourBankJson> Read(EmployeeHourBankScenarioBuilder s, int month, DateTime? cutoff = null, ResponseTimeEntryBalanceOperatorJson? supplied = null) =>
        s.Build().Read(s.Person.BranchId, s.Person, new DateTime(2026, month, 1), new DateTime(2026, month, DateTime.DaysInMonth(2026, month)), cutoff ?? new DateTime(2026, 11, 6), new DateTime(2026, 11, 6, 12, 0, 0), 1, 50, default, supplied);

    [Fact]
    public async Task Chronology_UsesOwnDatePolicies_ExactLedgerKinds_AndScopedAttendanceOnce()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(s.Entry(HourBankEntryKind.Opening, 10, Sep), s.Entry(HourBankEntryKind.Payment, -3, Oct.AddDays(1)), s.Entry(HourBankEntryKind.Adjustment, -2, Sep.AddDays(9), Oct.AddDays(2)));
        s.WithAttendance(s.Day(Sep.AddDays(9), TimeEntryStatus.Present, 10), s.Day(Sep.AddDays(10)), s.Day(Oct.AddDays(1), TimeEntryStatus.Present, 7));
        var result = await Read(s, 10);
        result.CarryOverHours.ShouldBe(4); result.AttendanceContributionHours.ShouldBe(1); result.PaymentHours.ShouldBe(-3); result.AdjustmentHours.ShouldBe(-2); result.ClosingBalanceHours.ShouldBe(0);
        result.AttendanceDays.Select(d => d.PolicyEffectiveFrom).ShouldBe([DateTime.MinValue, DateTime.MinValue, Oct]);
        await s.Records.Received(1).List<HourBankEntry>(s.Person.BranchId, Arg.Any<CancellationToken>());
        await s.Attendance.Received(1).ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(s.Person.BranchId, s.Person.Id, Sep, new DateTime(2026, 10, 31), Arg.Any<CancellationToken>());
        await s.Records.DidNotReceiveWithAnyArgs().Add(Arg.Any<HourBankEntry>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task PostedCutoff_ExcludesLaterMovementsAndDates_NotJustTheirDisplayedRows()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(s.Entry(HourBankEntryKind.Opening, 10, Sep), s.Entry(HourBankEntryKind.Payment, -3, Oct.AddDays(1)), s.Entry(HourBankEntryKind.Adjustment, -2, Sep.AddDays(9), Oct.AddDays(2)));
        s.WithAttendance(s.Day(Sep.AddDays(9), TimeEntryStatus.Present, 10), s.Day(Sep.AddDays(10)), s.Day(Oct.AddDays(1), TimeEntryStatus.Present, 7));
        var result = await Read(s, 10, Oct.AddDays(1));
        result.DateTo.ShouldBe(Oct.AddDays(1)); result.CarryOverHours.ShouldBe(4); result.PaymentHours.ShouldBe(-3); result.AdjustmentHours.ShouldBe(0); result.ClosingBalanceHours.ShouldBe(2); result.EntryCount.ShouldBe(2);
    }
    [Fact]
    public async Task LateAnchor_IsCarryoverEvidence_NotTwoCredits_AndEarlierUnknownRemainsUnknown()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(s.Entry(HourBankEntryKind.Opening, 10, Sep, Oct)); s.WithAttendance(s.Day(Sep.AddDays(9)));
        var old = await Read(s, 9, new DateTime(2026, 9, 30)); old.ClosingBalanceHours.ShouldBeNull(); old.Completeness.ShouldBe(PeopleSourceCompleteness.NotConfigured);
        var current = await Read(s, 10); current.CarryOverHours.ShouldBe(2); current.PeriodOpeningHours.ShouldBe(0); current.ClosingBalanceHours.ShouldBe(2);
    }
    [Fact]
    public async Task OpeningCorrection_IsAnOpenPeriodDelta_NotAReplacementOfPriorCarryover()
    {
        var s = new EmployeeHourBankScenarioBuilder(); var anchor = s.Entry(HourBankEntryKind.Opening, 10, Sep);
        s.WithLedger(anchor, s.Entry(HourBankEntryKind.Opening, 12, Sep, Oct.AddDays(2), anchor.Id, contribution: 2));
        var result = await Read(s, 10); result.CarryOverHours.ShouldBe(10); result.PeriodOpeningHours.ShouldBe(2); result.ClosingBalanceHours.ShouldBe(12);
        (await Read(s, 9, new DateTime(2026, 9, 30))).ClosingBalanceHours.ShouldBe(10);
    }
    [Theory]
    [InlineData(0, 0.0001, 0.0001)]
    [InlineData(-2, -0.0001, -2.0001)]
    public async Task EvidencedZeroNegativeAndSubMinuteHours_AreNotDisplayRounded(decimal opening, decimal adjustment, decimal closing)
    {
        var s = new EmployeeHourBankScenarioBuilder(); s.WithLedger(s.Entry(HourBankEntryKind.Opening, opening, Sep), s.Entry(HourBankEntryKind.Adjustment, adjustment, Oct));
        var result = await Read(s, 10); result.CarryOverHours.ShouldBe(opening); result.ClosingBalanceHours.ShouldBe(closing); result.Completeness.ShouldBe(PeopleSourceCompleteness.Complete);
    }
    [Fact]
    public async Task MissingAnchor_DoesNotVerifyZero_AndForeignPersonMovementsStayExcluded()
    {
        var s = new EmployeeHourBankScenarioBuilder(); var foreign = new HourBankEntry { OperatorId = Guid.NewGuid(), Kind = HourBankEntryKind.Opening, SignedContribution = 999, SourceDate = Sep, PostedOn = Sep };
        s.WithLedger(foreign); var result = await Read(s, 10); result.EntryCount.ShouldBe(0); result.CarryOverHours.ShouldBeNull(); result.ClosingBalanceHours.ShouldBeNull(); result.MissingInput.ShouldNotBeEmpty();
    }
    [Fact]
    public async Task SuppliedPeriodAttendance_RespectsAnchorAndCutoff_ForLiveProgressFlags()
    {
        var s = new EmployeeHourBankScenarioBuilder(); s.WithLedger(s.Entry(HourBankEntryKind.Opening, 10, Oct));
        var supplied = new ResponseTimeEntryBalanceOperatorJson { ContainsInProgress = true, Days = [new() { Date = Oct, BalanceHours = 1 }, new() { Date = Oct.AddDays(2), BalanceHours = 100, IsInProgress = true }] };
        var result = await Read(s, 10, Oct.AddDays(1), supplied);
        result.AttendanceContributionHours.ShouldBe(1); result.ClosingBalanceHours.ShouldBe(11); result.ContainsInProgress.ShouldBeFalse(); result.Completeness.ShouldBe(PeopleSourceCompleteness.Complete);
    }
    [Fact]
    public async Task IncludedOpenAttendance_KeepsProvisionalCompleteness()
    {
        var s = new EmployeeHourBankScenarioBuilder(); s.WithLedger(s.Entry(HourBankEntryKind.Opening, 0, Oct));
        var supplied = new ResponseTimeEntryBalanceOperatorJson { ContainsInProgress = true, Days = [new() { Date = Oct, BalanceHours = -1, IsInProgress = true }] };
        var result = await Read(s, 10, Oct.AddDays(1), supplied); result.ContainsInProgress.ShouldBeTrue(); result.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete); result.ClosingBalanceHours.ShouldBe(-1);
    }

    [Fact]
    public async Task FirstDayAnchorAndMovements_ArePeriodFlows_NotCarryover()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        var anchor = s.Entry(HourBankEntryKind.Opening, 10, Oct);
        s.WithLedger(anchor, s.Entry(HourBankEntryKind.Payment, -3, Oct), s.Entry(HourBankEntryKind.Adjustment, 2, Oct));
        s.WithAttendance(s.Day(Oct, TimeEntryStatus.Present, 7));
        var result = await Read(s, 10, Oct);
        result.CarryOverHours.ShouldBeNull(); result.PeriodOpeningHours.ShouldBe(10);
        result.PaymentHours.ShouldBe(-3); result.AdjustmentHours.ShouldBe(2); result.ClosingBalanceHours.ShouldBe(10);
        result.OpeningSourceId.ShouldBe(anchor.Id); result.OpeningSourceDate.ShouldBe(Oct); result.MissingInput.ShouldBeEmpty();
    }

    [Fact]
    public async Task PriorAnchor_FirstPeriodDayAttendance_IsNotPartOfCarryover()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(s.Entry(HourBankEntryKind.Opening, 10, Sep));
        s.WithAttendance(s.Day(Oct, TimeEntryStatus.Present, 7));
        var result = await Read(s, 10);
        result.CarryOverHours.ShouldBe(10); result.AttendanceContributionHours.ShouldBe(1); result.ClosingBalanceHours.ShouldBe(11);
    }

    [Fact]
    public async Task PeopleWithHistory_UsesInclusivePostedCutoff_AndDistinctIdentity()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(s.Entry(HourBankEntryKind.Opening, 0, Sep), s.Entry(HourBankEntryKind.Adjustment, 1, Oct),
            new HourBankEntry { OperatorId = Guid.NewGuid(), PostedOn = Oct.AddDays(1) });
        (await s.Build().PeopleWithHistory(s.Person.BranchId, Oct, default)).ShouldBe([s.Person.Id]);
    }

    [Fact]
    public async Task LedgerPaging_IsStableAndDoesNotLimitBalances()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        var anchor = s.Entry(HourBankEntryKind.Opening, 10, Sep);
        var rows = Enumerable.Range(0, 5).Select(i => s.Entry(HourBankEntryKind.Adjustment, 1, Oct.AddDays(i))).ToArray();
        s.WithLedger([anchor, .. rows]);
        var result = await s.Build().Read(s.Person.BranchId, s.Person, Oct, Oct.AddDays(29), Oct.AddDays(29), Oct.AddDays(29), 2, 2, default);
        result.EntryCount.ShouldBe(6); result.Entries.Select(x => x.Id).ShouldBe([rows[1].Id, rows[2].Id]);
        result.CarryOverHours.ShouldBe(10); result.AdjustmentHours.ShouldBe(5); result.ClosingBalanceHours.ShouldBe(15);
    }

    [Fact]
    public async Task OversizedLedger_RefusesPartialSuccess()
    {
        var s = new EmployeeHourBankScenarioBuilder();
        s.WithLedger(Enumerable.Range(0, 10001).Select(_ => s.Entry(HourBankEntryKind.Adjustment, 1, Oct)).ToArray());
        await Should.ThrowAsync<server.Exceptions.Exceptions.ConflictException>(() => Read(s, 10));
    }
}
