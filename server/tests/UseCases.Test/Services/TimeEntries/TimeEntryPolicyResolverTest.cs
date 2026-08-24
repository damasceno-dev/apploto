using CommonTestUtilities.Entities;
using server.Application.Services.TimeEntries;
using server.Domain.Entities;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.TimeEntries;

/// <summary>
/// Per-date policy resolution (M7.7 Phase 7 / §6.7): the applicable row is the greatest
/// <c>EffectiveFrom</c> on or before the entry date, boundary inclusive.
/// </summary>
public class TimeEntryPolicyResolverTest
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly DateTime BoundaryDate = new(2026, 8, 10);

    private static TimeEntryPolicy Policy(DateTime effectiveFrom, decimal dailyTargetHours)
    {
        return new TimeEntryPolicyBuilder()
            .WithBranchId(BranchId)
            .WithEffectiveFrom(effectiveFrom)
            .WithDailyTargetHours(dailyTargetHours)
            .Build();
    }

    [Fact]
    public void Resolve_ShouldReturnInitialRow_ForAnyDateBeforeLaterPolicies()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var newer = Policy(BoundaryDate, 8m);

        var resolved = TimeEntryPolicyResolver.Resolve([initial, newer], BoundaryDate.AddYears(-3));

        resolved.ShouldBeSameAs(initial);
    }

    [Fact]
    public void Resolve_ShouldReturnOlderRow_ForTheDayBeforeEffectiveFrom()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var newer = Policy(BoundaryDate, 8m);

        var resolved = TimeEntryPolicyResolver.Resolve([initial, newer], BoundaryDate.AddDays(-1));

        resolved.ShouldBeSameAs(initial);
    }

    [Fact]
    public void Resolve_ShouldReturnNewRow_OnItsEffectiveFromDate()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var newer = Policy(BoundaryDate, 8m);

        // EffectiveFrom is inclusive: the day the change was made already uses it.
        var resolved = TimeEntryPolicyResolver.Resolve([initial, newer], BoundaryDate);

        resolved.ShouldBeSameAs(newer);
    }

    [Fact]
    public void Resolve_ShouldReturnLatestRow_ForDatesAfterTheLastChange()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var middle = Policy(BoundaryDate.AddMonths(-1), 7.5m);
        var newest = Policy(BoundaryDate, 8m);

        var resolved = TimeEntryPolicyResolver.Resolve([initial, middle, newest], BoundaryDate.AddYears(5));

        resolved.ShouldBeSameAs(newest);
    }

    [Fact]
    public void Resolve_ShouldPickTheRowBetweenTwoChanges_NotTheLatest()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var middle = Policy(BoundaryDate.AddMonths(-1), 7.5m);
        var newest = Policy(BoundaryDate, 8m);

        var resolved = TimeEntryPolicyResolver.Resolve([initial, middle, newest], BoundaryDate.AddDays(-5));

        resolved.ShouldBeSameAs(middle);
    }

    [Fact]
    public void Resolve_ShouldIgnoreListOrder_WhenRowsArriveUnsorted()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var newer = Policy(BoundaryDate, 8m);

        var resolved = TimeEntryPolicyResolver.Resolve([newer, initial], BoundaryDate.AddDays(-1));

        resolved.ShouldBeSameAs(initial);
    }

    [Fact]
    public void Resolve_ShouldNormalizeTimeOfDay_ToTheEntryDate()
    {
        var initial = Policy(DateTime.MinValue, 7.33m);
        var newer = Policy(BoundaryDate, 8m);

        // A date-with-time input resolves like its calendar date.
        var resolved = TimeEntryPolicyResolver.Resolve([initial, newer], BoundaryDate.AddHours(23));

        resolved.ShouldBeSameAs(newer);
    }

    [Fact]
    public void Resolve_ShouldThrowInvariantBreach_WhenNoRowApplies()
    {
        var futureOnly = Policy(BoundaryDate, 8m);

        var exception = Should.Throw<InvalidOperationException>(
            () => TimeEntryPolicyResolver.Resolve([futureOnly], BoundaryDate.AddDays(-1)));

        exception.Message.ShouldContain("TimeEntryPolicy row missing");
    }
}
