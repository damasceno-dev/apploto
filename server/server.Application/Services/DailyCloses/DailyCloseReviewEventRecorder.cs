using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

/// <summary>
/// Stages immutable review events (gap X) on the caller's unit of work, so they commit or roll back
/// with the command that caused them. Values and names are captured as they are at that moment.
/// </summary>
public sealed class DailyCloseReviewEventRecorder(
    IDailyCloseReviewEventsRepository reviewEventsRepository,
    IProductsRepository productsRepository) : IDailyCloseReviewEventRecorder
{
    public Task RecordRejection(
        DailyClose rejectedClose,
        string rejectionReason,
        User actor,
        Operator? actorOperator,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        return reviewEventsRepository.Add(new DailyCloseReviewEvent
        {
            BranchId = rejectedClose.BranchId,
            DailyCloseId = rejectedClose.Id,
            Type = DailyCloseReviewEventType.Rejected,
            OccurredAt = occurredAt,
            ActorUserId = actor.Id,
            ActorUserName = actor.Name,
            ActorOperatorId = actorOperator?.Id,
            ActorOperatorName = actorOperator?.Name,
            RejectionReason = rejectionReason
        }, ct);
    }

    public async Task RecordOpeningChange(
        DailyClose source,
        uint sourceVersion,
        Guid? previousSourceDailyCloseId,
        IReadOnlyDictionary<Guid, decimal> openingBefore,
        IReadOnlyDictionary<Guid, decimal> openingAfter,
        DailyCloseSuccessorInvalidation successor,
        User actor,
        Operator? actorOperator,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        var changedProductIds = openingBefore.Keys
            .Union(openingAfter.Keys)
            .Where(productId => ValueOrNull(openingBefore, productId) != ValueOrNull(openingAfter, productId))
            .ToList();

        // Retired products keep their name: a before-value may belong to a product retired since it was counted.
        var productNames = changedProductIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await productsRepository.ListByIdsAndBranchIdAsNoTracking(changedProductIds, source.BranchId, ct))
                .ToDictionary(product => product.Id, product => product.Name);

        await reviewEventsRepository.Add(new DailyCloseReviewEvent
        {
            BranchId = source.BranchId,
            DailyCloseId = successor.DailyClose.Id,
            Type = DailyCloseReviewEventType.OpeningChanged,
            OccurredAt = occurredAt,
            ActorUserId = actor.Id,
            ActorUserName = actor.Name,
            ActorOperatorId = actorOperator?.Id,
            ActorOperatorName = actorOperator?.Name,
            SourceDailyCloseId = source.Id,
            SourceVersion = sourceVersion,
            PreviousSourceDailyCloseId = previousSourceDailyCloseId,
            AffectedStatusBefore = successor.PreviousStatus,
            AffectedStatusAfter = successor.DailyClose.Status,
            Items = changedProductIds
                .Select(productId => new DailyCloseReviewEventItem
                {
                    ProductId = productId,
                    ProductName = productNames.GetValueOrDefault(productId, string.Empty),
                    BeforeValue = ValueOrNull(openingBefore, productId),
                    AfterValue = ValueOrNull(openingAfter, productId)
                })
                .ToList()
        }, ct);
    }

    private static decimal? ValueOrNull(IReadOnlyDictionary<Guid, decimal> values, Guid productId)
    {
        return values.TryGetValue(productId, out var value) ? value : null;
    }
}
