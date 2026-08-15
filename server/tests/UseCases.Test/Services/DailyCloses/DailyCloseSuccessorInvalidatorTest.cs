using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseSuccessorInvalidatorTest
{
    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task InvalidateNextEligible_ShouldDemoteSupportedStatusAndReturnTransition(
        DailyCloseStatus status)
    {
        var now = new DateTime(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);
        var trigger = new DailyCloseBuilder()
            .WithDate(new DateTime(2026, 8, 10))
            .Build();
        var successor = new DailyCloseBuilder()
            .WithBranchId(trigger.BranchId)
            .WithAccountId(trigger.AccountId)
            .WithDate(new DateTime(2026, 8, 11))
            .WithStatus(status)
            .WithRejectionReason(status == DailyCloseStatus.Rejected ? "retain" : null)
            .Build();
        var repository = new DailyClosesRepositoryBuilder()
            .GetNextEligibleAfterDateByBranchIdAndAccountIdReturns(
                trigger.BranchId,
                trigger.AccountId,
                trigger.Date,
                successor)
            .Build();

        var result = await new DailyCloseSuccessorInvalidator(
                repository,
                new DailyCloseDraftTransition())
            .InvalidateNextEligible(trigger, now, Guid.NewGuid());

        result.ShouldNotBeNull();
        result.PreviousStatus.ShouldBe(status);
        result.DailyClose.ShouldBeSameAs(successor);
        successor.Status.ShouldBe(DailyCloseStatus.Draft);
        successor.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(trigger.Id);
        successor.RejectionReason.ShouldBe(status == DailyCloseStatus.Rejected ? "retain" : null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidateNextEligible_ShouldNoOpForMissingOrDraftSuccessor(bool hasDraft)
    {
        var trigger = new DailyCloseBuilder().Build();
        var successor = hasDraft
            ? new DailyCloseBuilder().WithStatus(DailyCloseStatus.Draft).Build()
            : null;
        var repository = Substitute.For<IDailyClosesRepository>();
        repository.GetNextEligibleAfterDateByBranchIdAndAccountId(
                trigger.BranchId,
                trigger.AccountId,
                trigger.Date,
                Arg.Any<CancellationToken>())
            .Returns(successor);
        var transition = Substitute.For<IDailyCloseDraftTransition>();

        var result = await new DailyCloseSuccessorInvalidator(repository, transition)
            .InvalidateNextEligible(trigger, DateTime.UtcNow, Guid.NewGuid());

        result.ShouldBeNull();
        transition.DidNotReceiveWithAnyArgs().ApplyOpeningRecheck(null!, null!, default, Guid.Empty);
    }
}
