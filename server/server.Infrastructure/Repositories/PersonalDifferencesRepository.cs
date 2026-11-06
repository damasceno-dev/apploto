using Microsoft.EntityFrameworkCore;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class PersonalDifferencesRepository(ServerDbContext db) : IPersonalDifferencesRepository
{
    public async Task<PersonalDifferencePage> Read(
        Guid branchId, Guid varianceProductId, DateTime from, DateTime to,
        Guid? operatorId, int page, int pageSize, CancellationToken ct = default)
    {
        var official = db.DailyCloseItems.AsNoTracking().Where(i => i.Active && i.ProductId == varianceProductId &&
            i.DailyClose.BranchId == branchId && i.DailyClose.Active && i.DailyClose.Status != DailyCloseStatus.Draft)
            .Select(i => new PersonalDifferenceEntry
            {
                SourceId = i.DailyCloseId, SourceKind = PersonalDifferenceSourceKind.OfficialSessionVariance, SessionId = i.DailyCloseId,
                OperatorId = i.DailyClose.CustodianOperatorId, SourceDate = i.DailyClose.Date, PostedOn = i.DailyClose.Date,
                ThresholdShortageContribution = i.DailyClose.CustodianOperatorId != null && i.Value < 0 ? -i.Value : 0m,
                SourceSignedAmount = i.Value, Shortage = i.DailyClose.CustodianOperatorId != null && i.Value < 0 ? -i.Value : 0m, Overage = i.DailyClose.CustodianOperatorId != null && i.Value > 0 ? i.Value : 0m, ComparisonRevision = null
            });
        var openings = db.SessionOpeningDiscrepancies.AsNoTracking().Where(d => d.Active && d.BranchId == branchId)
            .Select(d => new PersonalDifferenceEntry
            {
                SourceId = d.Id, SourceKind = PersonalDifferenceSourceKind.OpeningDiscrepancy, SessionId = d.SessionId,
                OperatorId = d.AttributedRevision != d.CurrentRevision ? null :
                    d.Attribution == OpeningDiscrepancyAttribution.Incoming ? d.Session.CustodianOperatorId :
                    d.Attribution == OpeningDiscrepancyAttribution.Outgoing ? db.DailyCloses.Where(s => s.Id == d.PredecessorSessionId)
                        .Select(s => s.CustodianOperatorId).FirstOrDefault() : null,
                SourceDate = d.Session.Date, PostedOn = d.Session.Date,
                ThresholdShortageContribution = d.AttributedRevision == d.CurrentRevision &&
                    (d.Attribution == OpeningDiscrepancyAttribution.Incoming || d.Attribution == OpeningDiscrepancyAttribution.Outgoing) && d.CurrentTotalDelta < 0
                    ? -d.CurrentTotalDelta : 0m,
                SourceSignedAmount = d.CurrentTotalDelta,
                Shortage = d.AttributedRevision == d.CurrentRevision &&
                    (d.Attribution == OpeningDiscrepancyAttribution.Incoming || d.Attribution == OpeningDiscrepancyAttribution.Outgoing) && d.CurrentTotalDelta < 0
                    ? -d.CurrentTotalDelta : 0m,
                Overage = d.AttributedRevision == d.CurrentRevision &&
                    (d.Attribution == OpeningDiscrepancyAttribution.Incoming || d.Attribution == OpeningDiscrepancyAttribution.Outgoing) && d.CurrentTotalDelta > 0
                    ? d.CurrentTotalDelta : 0m,
                ComparisonRevision = d.CurrentRevision
            });
        var added = db.OpeningDiscrepancyAdjustments.AsNoTracking().Where(a => a.BranchId == branchId && a.OperatorId != null)
            .Select(a => new PersonalDifferenceEntry
            {
                SourceId = a.Id, SourceKind = PersonalDifferenceSourceKind.OpeningDiscrepancyAdjustment, SessionId = a.SessionId,
                OperatorId = a.OperatorId, SourceDate = a.SourceDate, PostedOn = a.PostedOn,
                ThresholdShortageContribution = a.SignedAmount < 0 ? -a.SignedAmount : 0m,
                SourceSignedAmount = a.SignedAmount, Shortage = a.SignedAmount < 0 ? -a.SignedAmount : 0m,
                Overage = a.SignedAmount > 0 ? a.SignedAmount : 0m, ComparisonRevision = a.ComparisonRevision
            });
        var offsets = db.OpeningDiscrepancyAdjustments.AsNoTracking().Where(a => a.BranchId == branchId && a.PreviousOperatorId != null)
            .Select(a => new PersonalDifferenceEntry
            {
                SourceId = a.Id, SourceKind = PersonalDifferenceSourceKind.OpeningDiscrepancyAdjustmentOffset, SessionId = a.SessionId,
                OperatorId = a.PreviousOperatorId, SourceDate = a.SourceDate, PostedOn = a.PostedOn,
                // Cancel the threshold's previous contribution only when it was recognized in this window.
                // A credit correcting an older month cannot conceal a new official shortage this month.
                ThresholdShortageContribution = a.PreviousSignedAmount < 0 &&
                    (db.OpeningDiscrepancyAdjustments.Where(p => p.Id == a.PreviousAdjustmentId)
                        .Select(p => (DateTime?)p.PostedOn).FirstOrDefault() ?? a.SourceDate) >= from &&
                    (db.OpeningDiscrepancyAdjustments.Where(p => p.Id == a.PreviousAdjustmentId)
                        .Select(p => (DateTime?)p.PostedOn).FirstOrDefault() ?? a.SourceDate) <= to
                    ? a.PreviousSignedAmount : 0m,
                SourceSignedAmount = -a.PreviousSignedAmount, Shortage = a.PreviousSignedAmount < 0 ? a.PreviousSignedAmount : 0m,
                Overage = a.PreviousSignedAmount > 0 ? -a.PreviousSignedAmount : 0m, ComparisonRevision = a.ComparisonRevision
            });
        var query = official.Concat(openings).Concat(added).Concat(offsets)
            .Where(e => e.PostedOn >= from && e.PostedOn <= to && (operatorId == null || e.OperatorId == operatorId));
        var groups = await query.GroupBy(e => e.OperatorId).Select(g => new
        {
            OperatorId = g.Key, Shortage = g.Sum(e => e.Shortage), Overage = g.Sum(e => e.Overage), Count = g.Count(), ThresholdShortageBasis = g.Sum(e => e.ThresholdShortageContribution)
        }).ToListAsync(ct);
        var ids = groups.Where(g => g.OperatorId != null).Select(g => g.OperatorId!.Value).ToList();
        var people = await db.Operators.AsNoTracking().Where(o => o.BranchId == branchId && ids.Contains(o.Id))
            .Select(o => new { o.Id, o.Name, o.Active }).ToDictionaryAsync(o => o.Id, ct);
        var totals = groups.Select(g => new PersonalDifferenceTotal(g.OperatorId,
            g.OperatorId is { } id && people.TryGetValue(id, out var person) ? person.Name : null,
            g.OperatorId is { } personId && people.TryGetValue(personId, out var identity) ? identity.Active : null,
            g.Shortage, g.Overage, g.Count, g.ThresholdShortageBasis)).ToList();
        var count = await query.CountAsync(ct);
        var entries = await query.OrderBy(e => e.PostedOn).ThenBy(e => e.SourceKind).ThenBy(e => e.SourceId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PersonalDifferencePage(totals, count, entries);
    }
}
