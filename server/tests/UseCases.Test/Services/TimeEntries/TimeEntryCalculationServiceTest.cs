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

    private static readonly DateTime EntryDate = new(2026, 5, 8);
    private static readonly DateTime BranchLocalNow = new(2026, 5, 8, 18, 0, 0);

    private static (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        IReadOnlyList<TimeEntrySegmentInput>? segments = null,
        DateTime? entryDate = null,
        DateTime? branchLocalNow = null)
    {
        return new TimeEntryCalculationService().Calculate(
            status,
            segments ?? [],
            entryDate ?? EntryDate,
            branchLocalNow ?? BranchLocalNow,
            DailyTarget,
            LunchOver6H,
            LunchOver4H);
    }

    public static TheoryData<string, IReadOnlyList<TimeEntrySegmentInput>, decimal> SingleClosedSegmentCases => new()
    {
        {
            "GrossOver6H_FullLunchDeducted",
            [new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(17))],
            8m
        },
        {
            "GrossOver4HAndUpTo6H_Over4HLunchDeducted",
            [new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(13.5))],
            5.25m
        },
        {
            "GrossUpTo4H_NoLunchDeducted",
            [new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(11))],
            3m
        },
        {
            "Exactly4HGross_NoLunchDeduction",
            [new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(12))],
            4m
        },
        {
            "Exactly6HGross_Over4HLunchDeducted",
            [new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(14))],
            5.75m
        }
    };

    [Theory]
    [MemberData(nameof(SingleClosedSegmentCases))]
    public void Calculate_PresentWithSingleClosedSegment_AppliesLunchTier(
        string _,
        IReadOnlyList<TimeEntrySegmentInput> segments,
        decimal expectedTotal)
    {
        var (total, balance) = Calculate(TimeEntryStatus.Present, segments);

        total.ShouldBe(expectedTotal, tolerance: 0.001m);
        balance.ShouldBe(expectedTotal - DailyTarget, tolerance: 0.001m);
    }

    public static TheoryData<string, IReadOnlyList<TimeEntrySegmentInput>, decimal> MultiSegmentTopUpCases => new()
    {
        {
            "TenMinuteGapOnNineHourGross_TopsUpFiftyMinutes",
            [
                new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(12)),
                new TimeEntrySegmentInput(EntryDate.AddHours(12).AddMinutes(10), EntryDate.AddHours(17).AddMinutes(10))
            ],
            8m + 10m / 60m
        },
        {
            "ThirtyMinuteGapOnNineHourGross_TopsUpThirtyMinutes",
            [
                new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(12)),
                new TimeEntrySegmentInput(EntryDate.AddHours(12.5), EntryDate.AddHours(17.5))
            ],
            8.5m
        },
        {
            "OneHourGapOnNineHourGross_MeetsTierExactly",
            [
                new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(12)),
                new TimeEntrySegmentInput(EntryDate.AddHours(13), EntryDate.AddHours(18))
            ],
            9m
        },
        {
            "FiveHourGapOnSevenHourGross_ExceedsTier",
            [
                new TimeEntrySegmentInput(EntryDate.AddHours(8), EntryDate.AddHours(11)),
                new TimeEntrySegmentInput(EntryDate.AddHours(16), EntryDate.AddHours(20))
            ],
            7m
        }
    };

    [Theory]
    [MemberData(nameof(MultiSegmentTopUpCases))]
    public void Calculate_PresentWithMultipleClosedSegments_AppliesGapAwareTopUp(
        string _,
        IReadOnlyList<TimeEntrySegmentInput> segments,
        decimal expectedTotal)
    {
        var (total, balance) = Calculate(TimeEntryStatus.Present, segments);

        total.ShouldBe(expectedTotal, tolerance: 0.001m);
        balance.ShouldBe(expectedTotal - DailyTarget, tolerance: 0.001m);
    }

    [Fact]
    public void Calculate_LiveRunningWithGapBeforeOpenSegment_CountsGap()
    {
        var branchLocalNow = EntryDate.AddHours(17);
        var segments = new List<TimeEntrySegmentInput>
        {
            new(EntryDate.AddHours(8), EntryDate.AddHours(12)),
            new(EntryDate.AddHours(14), null)
        };

        var (total, balance) = Calculate(TimeEntryStatus.Present, segments, EntryDate, branchLocalNow);

        total.ShouldBe(7m, tolerance: 0.001m);
        balance.ShouldBe(7m - DailyTarget, tolerance: 0.001m);
    }

    [Fact]
    public void Calculate_SingleSegmentOvernight_UsesDateTimeSubtraction()
    {
        var entryDate = new DateTime(2026, 5, 8);
        var segments = new List<TimeEntrySegmentInput>
        {
            new(new DateTime(2026, 5, 8, 22, 0, 0), new DateTime(2026, 5, 9, 6, 0, 0))
        };

        var (total, balance) = Calculate(TimeEntryStatus.Present, segments, entryDate, new DateTime(2026, 5, 9, 6, 0, 0));

        total.ShouldBe(7m, tolerance: 0.001m);
        balance.ShouldBe(7m - DailyTarget, tolerance: 0.001m);
    }

    [Fact]
    public void Calculate_MultiSegmentOvernight_AppliesGapAwareLunch()
    {
        var entryDate = new DateTime(2026, 5, 8);
        var segments = new List<TimeEntrySegmentInput>
        {
            new(new DateTime(2026, 5, 8, 22, 0, 0), new DateTime(2026, 5, 9, 2, 0, 0)),
            new(new DateTime(2026, 5, 9, 3, 0, 0), new DateTime(2026, 5, 9, 6, 0, 0))
        };

        var (total, balance) = Calculate(TimeEntryStatus.Present, segments, entryDate, new DateTime(2026, 5, 9, 6, 0, 0));

        total.ShouldBe(7m, tolerance: 0.001m);
        balance.ShouldBe(7m - DailyTarget, tolerance: 0.001m);
    }

    [Fact]
    public void Calculate_ForgottenPriorDayOpenWithCompletedSibling_IgnoresOpenSegment()
    {
        var entryDate = new DateTime(2026, 5, 6);
        var segments = new List<TimeEntrySegmentInput>
        {
            new(entryDate.AddHours(8), entryDate.AddHours(12)),
            new(entryDate.AddHours(14), null)
        };

        var (total, balance) = Calculate(
            TimeEntryStatus.Present,
            segments,
            entryDate,
            new DateTime(2026, 5, 7, 17, 0, 0));

        total.ShouldBe(4m, tolerance: 0.001m);
        balance.ShouldBe(4m - DailyTarget, tolerance: 0.001m);
    }

    [Fact]
    public void Calculate_CurrentDayOpenAtBranchLocalNow_ReturnsZeroAndNegativeTarget()
    {
        var branchLocalNow = EntryDate.AddHours(8);
        var segments = new List<TimeEntrySegmentInput>
        {
            new(EntryDate.AddHours(8), null)
        };

        var (total, balance) = Calculate(TimeEntryStatus.Present, segments, EntryDate, branchLocalNow);

        total.ShouldBe(0m);
        balance.ShouldBe(-DailyTarget);
    }

    [Fact]
    public void Calculate_CurrentDayOpenAfterBranchLocalNow_ReturnsZeroAndNegativeTarget()
    {
        var segments = new List<TimeEntrySegmentInput>
        {
            new(EntryDate.AddHours(22), null)
        };

        var (total, balance) = Calculate(
            TimeEntryStatus.Present,
            segments,
            EntryDate,
            EntryDate.AddHours(8));

        total.ShouldBe(0m);
        balance.ShouldBe(-DailyTarget);
    }

    public static TheoryData<string, TimeEntryStatus, decimal, decimal> NonPresentCases => new()
    {
        { "Sunday", TimeEntryStatus.Sunday, DailyTarget, 0m },
        { "Holiday", TimeEntryStatus.Holiday, DailyTarget, 0m },
        { "Vacation", TimeEntryStatus.Vacation, DailyTarget, 0m },
        { "JustifiedAbsence", TimeEntryStatus.JustifiedAbsence, DailyTarget, 0m },
        { "DayOff", TimeEntryStatus.DayOff, 0m, -DailyTarget },
        { "UnjustifiedAbsence", TimeEntryStatus.UnjustifiedAbsence, 0m, -DailyTarget }
    };

    [Theory]
    [MemberData(nameof(NonPresentCases))]
    public void Calculate_NonPresentStatusWithEmptySegments_ReturnsExistingBranches(
        string _,
        TimeEntryStatus status,
        decimal expectedTotal,
        decimal expectedBalance)
    {
        var (total, balance) = Calculate(status);

        total.ShouldBe(expectedTotal);
        balance.ShouldBe(expectedBalance);
    }
}
