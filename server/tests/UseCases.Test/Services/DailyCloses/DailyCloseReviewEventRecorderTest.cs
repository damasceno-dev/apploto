using CommonTestUtilities.Entities;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseReviewEventRecorderTest
{
    private static readonly DateTime Now = new(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordRejection_ShouldStageEventWithSnapshotNamesAndReason()
    {
        var (events, products) = Repositories();
        var close = new DailyCloseBuilder().Build();
        var actor = new UserBuilder().WithName("Gerente Ana").Build();
        var actorOperator = new OperatorBuilder().WithName("Ana (operadora)").Build();

        await new DailyCloseReviewEventRecorder(events, products)
            .RecordRejection(close, "Recontar bilhetes", actor, actorOperator, Now);

        await events.Received(1).Add(
            Arg.Is<DailyCloseReviewEvent>(reviewEvent =>
                reviewEvent.Type == DailyCloseReviewEventType.Rejected &&
                reviewEvent.BranchId == close.BranchId &&
                reviewEvent.DailyCloseId == close.Id &&
                reviewEvent.OccurredAt == Now &&
                reviewEvent.ActorUserId == actor.Id &&
                reviewEvent.ActorUserName == "Gerente Ana" &&
                reviewEvent.ActorOperatorId == actorOperator.Id &&
                reviewEvent.ActorOperatorName == "Ana (operadora)" &&
                reviewEvent.RejectionReason == "Recontar bilhetes" &&
                reviewEvent.SourceDailyCloseId == null &&
                reviewEvent.Items.Count == 0),
            Arg.Any<CancellationToken>());
        await products.DidNotReceiveWithAnyArgs().ListByIdsAndBranchIdAsNoTracking(default!, default);
    }

    [Fact]
    public async Task RecordOpeningChange_ShouldKeepOnlyChangedProducts_WithMissingDistinctFromZero()
    {
        var (events, products) = Repositories();
        var source = new DailyCloseBuilder().Build();
        var successor = new DailyCloseBuilder().WithBranchId(source.BranchId).WithStatus(DailyCloseStatus.Draft).Build();
        var unchanged = Guid.NewGuid();
        var corrected = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        var added = Guid.NewGuid();
        products.ListByIdsAndBranchIdAsNoTracking(Arg.Any<IEnumerable<Guid>>(), source.BranchId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new ProductBuilder().WithId(corrected).WithName("Dinheiro").Build(),
                new ProductBuilder().WithId(dropped).WithName("Raspadinha (retirado)").Build(),
                new ProductBuilder().WithId(added).WithName("Telesena").Build()
            ]);
        var previousSourceId = Guid.NewGuid();
        var actor = new UserBuilder().WithName("Gerente Ana").Build();

        await new DailyCloseReviewEventRecorder(events, products).RecordOpeningChange(
            source,
            42u,
            previousSourceId,
            new Dictionary<Guid, decimal> { [unchanged] = 5m, [corrected] = 1000m, [dropped] = 0m },
            new Dictionary<Guid, decimal> { [unchanged] = 5m, [corrected] = 1100m, [added] = 30m },
            new DailyCloseSuccessorInvalidation(successor, DailyCloseStatus.Draft, null),
            actor,
            null,
            Now);

        await products.Received(1).ListByIdsAndBranchIdAsNoTracking(
            Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { corrected, dropped, added }.OrderBy(id => id))),
            source.BranchId,
            Arg.Any<CancellationToken>());
        await events.Received(1).Add(
            Arg.Is<DailyCloseReviewEvent>(reviewEvent =>
                reviewEvent.Type == DailyCloseReviewEventType.OpeningChanged &&
                reviewEvent.DailyCloseId == successor.Id &&
                reviewEvent.SourceDailyCloseId == source.Id &&
                reviewEvent.SourceVersion == 42u &&
                reviewEvent.PreviousSourceDailyCloseId == previousSourceId &&
                reviewEvent.AffectedStatusBefore == DailyCloseStatus.Draft &&
                reviewEvent.AffectedStatusAfter == DailyCloseStatus.Draft &&
                reviewEvent.ActorOperatorId == null &&
                reviewEvent.ActorOperatorName == null &&
                reviewEvent.Items.Count == 3 &&
                reviewEvent.Items.Any(item => item.ProductId == corrected && item.ProductName == "Dinheiro" && item.BeforeValue == 1000m && item.AfterValue == 1100m) &&
                reviewEvent.Items.Any(item => item.ProductId == dropped && item.ProductName == "Raspadinha (retirado)" && item.BeforeValue == 0m && item.AfterValue == null) &&
                reviewEvent.Items.Any(item => item.ProductId == added && item.BeforeValue == null && item.AfterValue == 30m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordOpeningChange_ShouldStageEventWithoutItems_WhenOnlyTheSourceIdentityChanged()
    {
        var (events, products) = Repositories();
        var source = new DailyCloseBuilder().Build();
        var successor = new DailyCloseBuilder().WithBranchId(source.BranchId).WithStatus(DailyCloseStatus.Draft).Build();
        var productId = Guid.NewGuid();

        await new DailyCloseReviewEventRecorder(events, products).RecordOpeningChange(
            source,
            1u,
            Guid.NewGuid(),
            new Dictionary<Guid, decimal> { [productId] = 10m },
            new Dictionary<Guid, decimal> { [productId] = 10m },
            new DailyCloseSuccessorInvalidation(successor, DailyCloseStatus.Submitted, Now),
            new UserBuilder().Build(),
            null,
            Now);

        await products.DidNotReceiveWithAnyArgs().ListByIdsAndBranchIdAsNoTracking(default!, default);
        await events.Received(1).Add(
            Arg.Is<DailyCloseReviewEvent>(reviewEvent =>
                reviewEvent.Items.Count == 0 &&
                reviewEvent.AffectedStatusBefore == DailyCloseStatus.Submitted),
            Arg.Any<CancellationToken>());
    }

    private static (IDailyCloseReviewEventsRepository Events, IProductsRepository Products) Repositories()
    {
        return (Substitute.For<IDailyCloseReviewEventsRepository>(), Substitute.For<IProductsRepository>());
    }
}
