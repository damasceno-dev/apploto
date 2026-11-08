using server.Application.UseCases.People;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.Services.Reports;

public class EmployeeSpecialDrawReader(IEmployeeRecordsRepository records, IEmployeeStatementSources sources)
{
    public async Task<ResponseSpecialDrawTotalsJson> Read(
        Guid branch,
        Guid person,
        DateTime from,
        DateTime to,
        DateTime cutoff,
        CancellationToken ct)
    {
        to = to < cutoff ? to : cutoff;
        var allAllocations = (await records.List<SpecialDrawAllocation>(branch, ct)).Where(r => r.PostedOn <= to).ToList();
        var periodEffectIds = allAllocations.Where(a => a.PostedOn >= from).Select(a => a.SessionFieldEffectId).Distinct().ToArray();
        var postedBasis = await sources.CommissionBasis(branch, person, from, to, ct);
        if (postedBasis.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var fees = await sources.SpecialFees(branch, person, from, to, ct, periodEffectIds, postedBasis.Select(b => b.SessionId).OfType<Guid>().Distinct().ToArray());
        if (fees.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var history = (await records.List<SpecialDraw>(branch, ct)).Where(r => r.PostedOn <= to).ToList();
        if (history.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var byId = history.ToDictionary(d => d.Id);
        var roots = new Dictionary<Guid, Guid>();
        Guid Root(Guid id)
        {
            var path = new HashSet<Guid>();
            var current = id;
            while (!roots.ContainsKey(current))
            {
                if (!path.Add(current) || !byId.TryGetValue(current, out var draw)) throw new ConflictException(ResourcesErrorMessages.PEOPLE_SOURCE_UNAVAILABLE);
                if (draw.PreviousId is not { } parent) { roots[current] = current; break; }
                current = parent;
            }
            foreach (var ancestor in path) roots[ancestor] = roots[current];
            return roots[id];
        }
        foreach (var draw in history) Root(draw.Id);
        var names = PeopleHistory.Heads(history, d => d.PreviousId).ToDictionary(d => Root(d.Id), d => d.Name);
        var feeIds = fees.Select(f => f.Id).ToHashSet();
        var allocations = allAllocations.Where(r => feeIds.Contains(r.SessionFieldEffectId)).ToList();
        if (allocations.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var periodAllocations = allocations.Where(a => a.PostedOn >= from).ToList();
        var heads = PeopleHistory.Heads(allocations, r => r.PreviousId).Where(r => !r.Cancelled).ToList();
        var unknown = fees.Where(f => !f.Official || heads.Any(a => a.SessionFieldEffectId == f.Id && (a.SourceRevision != f.Revision || a.SourceValue != f.Value)) || heads.Where(a => a.SessionFieldEffectId == f.Id).Sum(a => a.Amount) > f.Value).Select(f => f.Id).ToList();
        var mappedTransactions = fees.Where(f => f.Official).SelectMany(f => (f.TransactionHistory ?? []).Concat(f.TransactionId is { } id ? [id] : [])).ToHashSet();
        var unmapped = postedBasis.Where(s => s.Component == server.Domain.Entities.Enums.CommissionBasisComponent.SpecialDrawServiceFee && !mappedTransactions.Contains(s.TransactionId ?? s.SourceId)).ToList();
        unknown.AddRange(unmapped.Select(s => s.SourceId));
        var allocated = periodAllocations.Sum(a => a.SignedContribution);
        var total = postedBasis.Where(s => s.Component == server.Domain.Entities.Enums.CommissionBasisComponent.SpecialDrawServiceFee).Sum(s => s.SignedAmount);
        return new ResponseSpecialDrawTotalsJson
        {
            AsOfDate = cutoff,
            History = history.Select(r => r.ToResponse()).ToList(),
            Allocations = periodAllocations.Select(r => r.ToResponse()).ToList(),
            UnmappedSourceFeeTotal = unmapped.Sum(s => s.SignedAmount),
            SourceFeeTotal = total,
            AllocatedFeeTotal = allocated,
            UnallocatedFeeTotal = total - allocated,
            SourceMappingComplete = unknown.Count == 0,
            UnmappedSourceIds = unknown,
            Draws = periodAllocations.GroupBy(a => Root(a.DrawId)).Select(g => new ResponseSpecialDrawTotalJson
            {
                DrawId = g.Key,
                Name = names[g.Key],
                AllocatedFee = g.Sum(a => a.SignedContribution)
            }).OrderBy(d => d.Name).ThenBy(d => d.DrawId).ToList()
        };
    }
}
