using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Receivables;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Receivables;

/// <summary>M9.7 Phase 1.2: OccurredOn may be locked; PostedOn is open, never future, never before OccurredOn.</summary>
public class SettlementEventDatingTest
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly DateTime Today = new(2025, 11, 5);
    private static readonly DateTime LockDate = new(2025, 10, 31);

    private static SettlementEventDating Create()
    {
        var clock = Substitute.For<IBranchClock>();
        var now = new DateTime(2025, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        clock.UtcNow().Returns(now);
        clock.LocalBusinessDate(now).Returns(Today);
        var settings = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(BranchId, new Setting { BranchId = BranchId, LockDate = LockDate })
            .Build();
        return new SettlementEventDating(clock, new LockDateGuard(new LockDateReader(settings)));
    }

    [Fact]
    public async Task Resolve_ShouldAcceptALockedOccurrence_AndPostOnTodayByDefault()
    {
        var dates = await Create().Resolve(BranchId, new DateTime(2025, 10, 15), postedOn: null);

        (dates.OccurredOn, dates.PostedOn, dates.Today).ShouldBe((new DateTime(2025, 10, 15), Today, Today));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseAPostingInTheLockedPeriod()
    {
        var error = await Should.ThrowAsync<ConflictException>(() =>
            Create().Resolve(BranchId, new DateTime(2025, 10, 15), new DateTime(2025, 10, 31)));
        error.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTLEMENT_POSTED_ON_LOCKED);
    }

    [Theory]
    [InlineData(2025, 11, 6, null, nameof(ResourcesErrorMessages.SETTLEMENT_OCCURRED_ON_IN_FUTURE))]
    [InlineData(2025, 11, 1, 6, nameof(ResourcesErrorMessages.SETTLEMENT_POSTED_ON_IN_FUTURE))]
    [InlineData(2025, 11, 4, 3, nameof(ResourcesErrorMessages.SETTLEMENT_POSTED_ON_BEFORE_OCCURRED_ON))]
    public async Task Resolve_ShouldRefuseFutureOrInvertedDates(int year, int month, int day, int? postedDay, string key)
    {
        DateTime? posted = postedDay is { } d ? new DateTime(2025, 11, d) : null;
        var error = await Should.ThrowAsync<OnValidationException>(() =>
            Create().Resolve(BranchId, new DateTime(year, month, day), posted));
        error.GetErrorMessages.ShouldContain(typeof(ResourcesErrorMessages).GetProperty(key)!.GetValue(null)!.ToString()!);
    }

    [Fact]
    public void EnsureChronology_ShouldRefuseAnEventBeforeTheReceivableOrItsPredecessorOnEitherDate()
    {
        var receivable = new Transaction { Date = new DateTime(2025, 9, 10), Value = 200m };
        var previous = new SettlementEvent { OccurredOn = new DateTime(2025, 9, 30), PostedOn = new DateTime(2025, 10, 2) };
        var today = new DateTime(2025, 11, 5);

        Should.Throw<ConflictException>(() => SettlementEventDating.EnsureChronology(
                receivable, null, new SettlementEventDates(new DateTime(2025, 9, 9), today, today)))
            .GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTLEMENT_OCCURRED_BEFORE_RECEIVABLE_DATE);
        Should.Throw<ConflictException>(() => SettlementEventDating.EnsureChronology(
                receivable, previous, new SettlementEventDates(new DateTime(2025, 9, 29), today, today)))
            .GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTLEMENT_CHRONOLOGY_INVALID);
        Should.Throw<ConflictException>(() => SettlementEventDating.EnsureChronology(
                receivable, previous, new SettlementEventDates(new DateTime(2025, 10, 15), new DateTime(2025, 10, 1), today)))
            .GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTLEMENT_CHRONOLOGY_INVALID);
        SettlementEventDating.EnsureChronology(
            receivable, previous, new SettlementEventDates(new DateTime(2025, 10, 15), today, today));
    }
}
