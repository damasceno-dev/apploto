using server.Application.Services.TimeEntries;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.TimeEntries;

public class TimeEntryCalculationServiceTest
{
    private const decimal DailyTarget = 7.33m;
    private const decimal LunchOver6H = 1.00m;
    private const decimal LunchOver4H = 0.25m;

    private static (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        TimeOnly? clockIn = null,
        TimeOnly? clockOut = null) =>
        new TimeEntryCalculationService().Calculate(
            status, clockIn, clockOut, DailyTarget, LunchOver6H, LunchOver4H);

    private static (decimal TotalHours, decimal BalanceHours) CalculateLiveRunning(
        TimeOnly clockIn,
        DateTime entryDate,
        DateTime branchLocalNow) =>
        new TimeEntryCalculationService().CalculateLiveRunning(
            clockIn, entryDate, branchLocalNow, DailyTarget, LunchOver6H, LunchOver4H);

    // ── Present — lunch tiers ─────────────────────────────────────────────

    public static TheoryData<string, TimeOnly, TimeOnly, decimal, decimal> PresentCases => new()
    {
        {
            "GrossOver6H_FullLunchDeducted",
            new TimeOnly(8, 0), new TimeOnly(17, 0),   // 9h gross → deduct 1h
            8.00m, 8.00m - DailyTarget
        },
        {
            "GrossOver4HAndUpTo6H_HalfLunchDeducted",
            new TimeOnly(8, 0), new TimeOnly(13, 30),  // 5.5h gross → deduct 0.25h
            5.25m, 5.25m - DailyTarget
        },
        {
            "GrossUpTo4H_NoLunchDeducted",
            new TimeOnly(8, 0), new TimeOnly(11, 0),   // 3h gross → no deduction
            3.00m, 3.00m - DailyTarget
        },
    };

    [Theory]
    [MemberData(nameof(PresentCases))]
    public void Calculate_Present_AppliesCorrectLunchTier(
        string _,
        TimeOnly clockIn,
        TimeOnly clockOut,
        decimal expectedTotal,
        decimal expectedBalance)
    {
        var (total, balance) = Calculate(TimeEntryStatus.Present, clockIn, clockOut);

        total.ShouldBe(expectedTotal, tolerance: 0.001m);
        balance.ShouldBe(expectedBalance, tolerance: 0.001m);
    }

    // ── Present — midnight crossing ───────────────────────────────────────

    [Fact]
    public void Calculate_Present_MidnightCrossing_Adds24Hours()
    {
        // 22:00 → 06:00 = 8h gross → deduct 1h → 7h total
        var (total, balance) = Calculate(TimeEntryStatus.Present, new TimeOnly(22, 0), new TimeOnly(6, 0));

        total.ShouldBe(7.00m, tolerance: 0.001m);
        balance.ShouldBe(7.00m - DailyTarget, tolerance: 0.001m);
    }

    // ── Present — exact boundary at 4h and 6h ────────────────────────────

    public static TheoryData<string, TimeOnly, TimeOnly, decimal, decimal> BoundaryCases => new()
    {
        {
            "Exactly4HGross_NoLunchDeduction",
            new TimeOnly(8, 0), new TimeOnly(12, 0),   // exactly 4h → no deduction
            4.00m, 4.00m - DailyTarget
        },
        {
            "Exactly6HGross_Over4HDeductionApplies_NotOver6H",
            new TimeOnly(8, 0), new TimeOnly(14, 0),   // exactly 6h → Over4H tier
            5.75m, 5.75m - DailyTarget
        },
    };

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void Calculate_Present_ExactBoundary_AppliesCorrectTier(
        string _,
        TimeOnly clockIn,
        TimeOnly clockOut,
        decimal expectedTotal,
        decimal expectedBalance)
    {
        var (total, balance) = Calculate(TimeEntryStatus.Present, clockIn, clockOut);

        total.ShouldBe(expectedTotal, tolerance: 0.001m);
        balance.ShouldBe(expectedBalance, tolerance: 0.001m);
    }

    // ── Abonado statuses — TotalHours = DailyTarget, Balance = 0 ─────────

    public static TheoryData<string, TimeEntryStatus> AbonadoCases => new()
    {
        { "Sunday",           TimeEntryStatus.Sunday },
        { "Holiday",          TimeEntryStatus.Holiday },
        { "Vacation",         TimeEntryStatus.Vacation },
        { "JustifiedAbsence", TimeEntryStatus.JustifiedAbsence },
    };

    [Theory]
    [MemberData(nameof(AbonadoCases))]
    public void Calculate_AbonadoStatus_ReturnsDailyTargetAndZeroBalance(string _, TimeEntryStatus status)
    {
        var (total, balance) = Calculate(status);

        total.ShouldBe(DailyTarget);
        balance.ShouldBe(0m);
    }

    // ── Owing statuses — TotalHours = 0, Balance = -DailyTarget ──────────

    public static TheoryData<string, TimeEntryStatus> OwingCases => new()
    {
        { "DayOff",             TimeEntryStatus.DayOff },
        { "UnjustifiedAbsence", TimeEntryStatus.UnjustifiedAbsence },
    };

    [Theory]
    [MemberData(nameof(OwingCases))]
    public void Calculate_OwingStatus_ReturnsZeroTotalAndNegativeBalance(string _, TimeEntryStatus status)
    {
        var (total, balance) = Calculate(status);

        total.ShouldBe(0m);
        balance.ShouldBe(-DailyTarget);
    }

    // ── CalculateLiveRunning ──────────────────────────────────────────────

    private static readonly DateTime SameDay = new DateTime(2026, 5, 7);

    public static TheoryData<string, TimeOnly, DateTime, DateTime, decimal, decimal> LiveRunningCases => new()
    {
        {
            "JustClockedIn_GrossZero_BalanceIsNegativeTarget",
            new TimeOnly(8, 0),
            SameDay,
            new DateTime(2026, 5, 7, 8, 0, 0),   // branchLocalNow = 08:00 same day
            0m, -DailyTarget
        },
        {
            "GrossLessThan4H_NoLunchDeduction",
            new TimeOnly(8, 0),
            SameDay,
            new DateTime(2026, 5, 7, 11, 0, 0),  // branchLocalNow = 11:00 → gross 3h
            3.00m, 3.00m - DailyTarget
        },
        {
            "GrossOver4HAndUpTo6H_Over4HLunchTier",
            new TimeOnly(8, 0),
            SameDay,
            new DateTime(2026, 5, 7, 13, 30, 0), // branchLocalNow = 13:30 → gross 5.5h
            5.25m, 5.25m - DailyTarget
        },
        {
            "GrossOver6H_Over6HLunchTier",
            new TimeOnly(8, 0),
            SameDay,
            new DateTime(2026, 5, 7, 17, 0, 0),  // branchLocalNow = 17:00 → gross 9h
            8.00m, 8.00m - DailyTarget
        },
    };

    [Theory]
    [MemberData(nameof(LiveRunningCases))]
    public void CalculateLiveRunning_SameDayEntry_AppliesCorrectLunchTier(
        string _,
        TimeOnly clockIn,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal expectedTotal,
        decimal expectedBalance)
    {
        var (total, balance) = CalculateLiveRunning(clockIn, entryDate, branchLocalNow);

        total.ShouldBe(expectedTotal, tolerance: 0.001m);
        balance.ShouldBe(expectedBalance, tolerance: 0.001m);
    }

    [Fact]
    public void CalculateLiveRunning_ForgottenClockOut_PriorDay_ReturnsZeroZero()
    {
        // entryDate = yesterday, branchLocalNow = today → forgotten clock-out path
        var clockIn = new TimeOnly(8, 0);
        var entryDate = new DateTime(2026, 5, 6);
        var branchLocalNow = new DateTime(2026, 5, 7, 8, 0, 0);

        var (total, balance) = CalculateLiveRunning(clockIn, entryDate, branchLocalNow);

        total.ShouldBe(0m);
        balance.ShouldBe(0m);
    }

    [Fact]
    public void CalculateLiveRunning_SameDay_EffectiveClockOutBeforeClockIn_ReturnsZeroAndNegativeTarget()
    {
        // ClockIn = 22:00 same day, branchLocalNow = 08:00 same day (clock drift / Manager manual date)
        // Without the guard this would trigger the midnight wrap and produce ~10h phantom work.
        var clockIn = new TimeOnly(22, 0);
        var entryDate = SameDay;
        var branchLocalNow = new DateTime(2026, 5, 7, 8, 0, 0);

        var (total, balance) = CalculateLiveRunning(clockIn, entryDate, branchLocalNow);

        total.ShouldBe(0m);
        balance.ShouldBe(-DailyTarget);
    }
}
