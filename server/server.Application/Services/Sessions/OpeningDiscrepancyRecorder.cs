using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

/// <summary>
/// Opens and revises opening discrepancies (§6.20). A comparison is acknowledged − proposed per product; a missing
/// proposed value compares as zero only once a real source exists. Revisions append; an earlier attribution stays as
/// history but stops counting toward a person until a manager attributes the new revision.
/// </summary>
public sealed class OpeningDiscrepancyRecorder(
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    SessionEventRecorder sessionEventRecorder)
{
    public async Task<SessionOpeningDiscrepancy?> OpenIfDifferent(
        DailyClose session,
        SessionOpening opening,
        Guid? predecessorSessionId,
        User actor,
        DateTime now,
        CancellationToken ct = default)
    {
        var comparison = Compare(
            opening.Items.ToDictionary(item => item.ProductId, item => item.ProposedValue ?? 0m),
            opening.Items.ToDictionary(item => item.ProductId, item => item.AcknowledgedValue));
        if (comparison.All(row => row.Delta == 0m))
            return null;

        var discrepancy = new SessionOpeningDiscrepancy
        {
            BranchId = session.BranchId,
            SessionOpeningId = opening.Id,
            SessionId = session.Id,
            HandoverId = opening.HandoverId,
            PredecessorSessionId = predecessorSessionId,
            SourceKind = opening.SourceKind,
            CurrentRevision = 1,
            CurrentTotalDelta = comparison.Sum(row => row.Delta),
            CreatedAt = now
        };
        discrepancy.Revisions.Add(BuildRevision(discrepancy, 1, opening.SourceId, opening.SourceRevision, comparison, actor, now));
        await discrepanciesRepository.Add(discrepancy, ct);
        await sessionEventRecorder.Record(
            session,
            SessionEventKind.OpeningDiscrepancyOpened,
            actor,
            now,
            new { DiscrepancyId = discrepancy.Id, Revision = 1, discrepancy.CurrentTotalDelta },
            relatedId: discrepancy.Id,
            ct: ct);
        return discrepancy;
    }

    /// <summary>
    /// Appends a comparison revision when the acknowledged successor's predecessor closing changed. The successor's
    /// opening is never substituted. Creates the discrepancy if the original comparison matched.
    /// </summary>
    public async Task ReviseForPredecessorChange(
        DailyClose successor,
        DailyClose predecessor,
        IReadOnlyDictionary<Guid, decimal> predecessorClosing,
        User actor,
        DateTime now,
        CancellationToken ct = default)
    {
        var opening = successor.Opening!;
        var acknowledged = opening.Items.ToDictionary(item => item.ProductId, item => item.AcknowledgedValue);
        var comparison = Compare(predecessorClosing, acknowledged);
        var total = comparison.Sum(row => row.Delta);

        var discrepancy = await discrepanciesRepository.GetBySessionId(successor.BranchId, successor.Id, ct);
        if (discrepancy is null)
        {
            if (comparison.All(row => row.Delta == 0m))
                return;

            discrepancy = new SessionOpeningDiscrepancy
            {
                BranchId = successor.BranchId,
                SessionOpeningId = opening.Id,
                SessionId = successor.Id,
                HandoverId = opening.HandoverId,
                PredecessorSessionId = predecessor.Id,
                SourceKind = opening.SourceKind,
                CurrentRevision = 1,
                CurrentTotalDelta = total,
                CreatedAt = now
            };
            discrepancy.Revisions.Add(BuildRevision(discrepancy, 1, predecessor.Id, predecessor.CountRevision, comparison, actor, now));
            await discrepanciesRepository.Add(discrepancy, ct);
            await sessionEventRecorder.Record(
                successor,
                SessionEventKind.OpeningDiscrepancyOpened,
                actor,
                now,
                new { DiscrepancyId = discrepancy.Id, Revision = 1, CurrentTotalDelta = total, PredecessorSessionId = predecessor.Id },
                relatedId: discrepancy.Id,
                ct: ct);
            return;
        }

        var previousTotal = discrepancy.CurrentTotalDelta;
        var revision = discrepancy.CurrentRevision + 1;
        discrepancy.CurrentRevision = revision;
        discrepancy.CurrentTotalDelta = total;
        discrepancy.PredecessorSessionId = predecessor.Id;
        await discrepanciesRepository.AddRevision(
            BuildRevision(discrepancy, revision, predecessor.Id, predecessor.CountRevision, comparison, actor, now),
            ct);
        await sessionEventRecorder.Record(
            successor,
            SessionEventKind.OpeningDiscrepancyRevised,
            actor,
            now,
            new { DiscrepancyId = discrepancy.Id, Revision = revision, PreviousTotalDelta = previousTotal, CurrentTotalDelta = total },
            relatedId: discrepancy.Id,
            ct: ct);
    }

    private static List<ComparisonRow> Compare(
        IReadOnlyDictionary<Guid, decimal> proposed,
        IReadOnlyDictionary<Guid, decimal> acknowledged)
    {
        return proposed.Keys
            .Union(acknowledged.Keys)
            .Order()
            .Select(productId =>
            {
                var proposedValue = proposed.GetValueOrDefault(productId);
                var acknowledgedValue = acknowledged.GetValueOrDefault(productId);
                return new ComparisonRow(productId, proposedValue, acknowledgedValue, acknowledgedValue - proposedValue);
            })
            .ToList();
    }

    private static SessionOpeningDiscrepancyRevision BuildRevision(
        SessionOpeningDiscrepancy discrepancy,
        int revision,
        Guid? sourceId,
        long? sourceRevision,
        IReadOnlyList<ComparisonRow> comparison,
        User actor,
        DateTime now)
    {
        var record = new SessionOpeningDiscrepancyRevision
        {
            DiscrepancyId = discrepancy.Id,
            Revision = revision,
            SourceId = sourceId,
            SourceRevision = sourceRevision,
            ProposedTotal = comparison.Sum(row => row.Proposed),
            AcknowledgedTotal = comparison.Sum(row => row.Acknowledged),
            TotalDelta = comparison.Sum(row => row.Delta),
            CreatedByUserId = actor.Id,
            CreatedAt = now
        };
        foreach (var row in comparison)
        {
            record.Items.Add(new SessionOpeningDiscrepancyRevisionItem
            {
                RevisionId = record.Id,
                ProductId = row.ProductId,
                ProposedValue = row.Proposed,
                AcknowledgedValue = row.Acknowledged,
                Delta = row.Delta,
                CreatedAt = now
            });
        }

        return record;
    }

    private sealed record ComparisonRow(Guid ProductId, decimal Proposed, decimal Acknowledged, decimal Delta);
}
