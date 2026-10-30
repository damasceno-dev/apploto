using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseSuccessorInvalidatorTest
{
    private static readonly DateTime Now = new(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task InvalidateNextEligible_ShouldDemoteInheritedSuccessorAndReturnTransition(DailyCloseStatus status)
    {
        var trigger = new DailyCloseBuilder().WithDate(new DateTime(2026, 8, 10)).Build();
        var successor = new DailyCloseBuilder()
            .WithBranchId(trigger.BranchId)
            .WithAccountId(trigger.AccountId)
            .WithDate(new DateTime(2026, 8, 11))
            .WithStatus(status)
            .WithRejectionReason(status == DailyCloseStatus.Rejected ? "retain" : null)
            .Build();
        var invalidator = CreateInvalidator(new DailyClosesRepositoryBuilder().GetDependentSuccessorReturns(trigger.Id, successor).Build());

        var result = await invalidator.InvalidateNextEligible(trigger, new Dictionary<Guid, decimal>(), Now, new UserBuilder().Build());

        result.ShouldNotBeNull();
        result.PreviousStatus.ShouldBe(status);
        result.DailyClose.ShouldBeSameAs(successor);
        result.Demoted.ShouldBeTrue();
        result.OpeningRecheckRequiredAt.ShouldBe(Now);
        successor.Status.ShouldBe(DailyCloseStatus.Draft);
        successor.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(trigger.Id);
        successor.RejectionReason.ShouldBe(status == DailyCloseStatus.Rejected ? "retain" : null);
    }

    [Fact]
    public async Task InvalidateNextEligible_ShouldReturnNull_WhenNoDependentSuccessorExists()
    {
        var trigger = new DailyCloseBuilder().Build();
        var repository = Substitute.For<IDailyClosesRepository>();
        repository.GetDependentSuccessor(Arg.Any<DailyClose>(), Arg.Any<CancellationToken>()).Returns((DailyClose?)null);

        var result = await CreateInvalidator(repository)
            .InvalidateNextEligible(trigger, new Dictionary<Guid, decimal>(), Now, new UserBuilder().Build());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidateNextEligible_ShouldLeaveDraftSuccessorUntouched()
    {
        var trigger = new DailyCloseBuilder().Build();
        var successor = new DailyCloseBuilder().WithStatus(DailyCloseStatus.Draft).Build();
        var invalidator = CreateInvalidator(new DailyClosesRepositoryBuilder().GetDependentSuccessorReturns(trigger.Id, successor).Build());

        var result = await invalidator.InvalidateNextEligible(trigger, new Dictionary<Guid, decimal>(), Now, new UserBuilder().Build());

        result.ShouldNotBeNull();
        result.Demoted.ShouldBeFalse();
        successor.OpeningRecheckRequiredAt.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidateNextEligible_ShouldReviseDiscrepancyAndNeverDemote_WhenSuccessorAcknowledgedItsOpening()
    {
        // Corrected outgoing 1,100 → 1,090 with incoming acknowledgment 1,080: comparison −20 → −10; the incoming
        // opening, variance and Approved status stay as they were (§6.20 correction lineage).
        var productId = Guid.NewGuid();
        var trigger = new DailyCloseBuilder().Build();
        var opening = new SessionOpening
        {
            SourceKind = SessionOpeningSourceKind.PreviousSession,
            SourceId = trigger.Id,
            Items = [new SessionOpeningItem { ProductId = productId, ProposedValue = 1_100m, AcknowledgedValue = 1_080m }]
        };
        var successor = new DailyCloseBuilder()
            .WithBranchId(trigger.BranchId)
            .WithAccountId(trigger.AccountId)
            .WithStatus(DailyCloseStatus.Approved)
            .WithOpening(opening)
            .Build();
        var discrepancy = new SessionOpeningDiscrepancy
        {
            SessionId = successor.Id,
            SessionOpeningId = opening.Id,
            CurrentRevision = 1,
            CurrentTotalDelta = -20m,
            Attribution = OpeningDiscrepancyAttribution.Incoming,
            AttributedRevision = 1
        };
        var discrepancies = Substitute.For<ISessionOpeningDiscrepanciesRepository>();
        discrepancies.GetBySessionId(successor.BranchId, successor.Id, Arg.Any<CancellationToken>()).Returns(discrepancy);
        var invalidator = CreateInvalidator(
            new DailyClosesRepositoryBuilder().GetDependentSuccessorReturns(trigger.Id, successor).Build(),
            discrepancies);

        var result = await invalidator.InvalidateNextEligible(
            trigger,
            new Dictionary<Guid, decimal> { [productId] = 1_090m },
            Now,
            new UserBuilder().Build());

        result.ShouldBeNull();
        successor.Status.ShouldBe(DailyCloseStatus.Approved);
        successor.OpeningRecheckRequiredAt.ShouldBeNull();
        discrepancy.CurrentRevision.ShouldBe(2);
        discrepancy.CurrentTotalDelta.ShouldBe(-10m);
        // The earlier attribution names revision 1, so it no longer counts toward a person.
        discrepancy.AttributedRevision.ShouldBe(1);
        await discrepancies.Received(1).AddRevision(
            Arg.Is<SessionOpeningDiscrepancyRevision>(revision => revision.Revision == 2 && revision.TotalDelta == -10m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateNextEligible_ShouldRejectTheCorrection_WhenTheDependentSuccessorIsLocked()
    {
        var trigger = new DailyCloseBuilder().WithDate(new DateTime(2026, 8, 10)).Build();
        var successor = new DailyCloseBuilder()
            .WithBranchId(trigger.BranchId)
            .WithDate(new DateTime(2026, 8, 11))
            .WithStatus(DailyCloseStatus.Approved)
            .Build();
        var settings = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                successor.BranchId,
                new Setting { BranchId = successor.BranchId, LockDate = successor.Date })
            .Build();
        var invalidator = new DailyCloseSuccessorInvalidator(
            new DailyClosesRepositoryBuilder().GetDependentSuccessorReturns(trigger.Id, successor).Build(),
            new DailyCloseDraftTransition(),
            SessionTestServices.DiscrepancyRecorder(),
            new LockDateGuard(new LockDateReader(settings)));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            invalidator.InvalidateNextEligible(trigger, new Dictionary<Guid, decimal>(), Now, new UserBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_LOCKED_SUCCESSOR);
        successor.Status.ShouldBe(DailyCloseStatus.Approved);
    }

    private static DailyCloseSuccessorInvalidator CreateInvalidator(
        IDailyClosesRepository repository,
        ISessionOpeningDiscrepanciesRepository? discrepancies = null)
    {
        return new DailyCloseSuccessorInvalidator(
            repository,
            new DailyCloseDraftTransition(),
            SessionTestServices.DiscrepancyRecorder(discrepancies),
            new LockDateGuard(new LockDateReader(new SettingsRepositoryBuilder().Build())));
    }
}
