using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.Services.Reports;

public class PersonalDifferencesReader(IPersonalDifferencesRepository repository, ISettingsRepository settings)
{
    public async Task<ResponsePersonalDifferencesJson> Read(
        Guid branchId, Guid varianceProductId, int year, int month, DateTime cutoff,
        Guid? operatorId = null, int page = 1, int pageSize = 50, bool applyCurrentThreshold = true, CancellationToken ct = default)
    {
        var (from, to, _) = MonthlyFinancialReader.MonthWindow(year, month, cutoff);
        var threshold = (await settings.GetByBranchIdAsNoTracking(branchId, ct))?.PersonalShortageThreshold;
        var result = await repository.Read(branchId, varianceProductId, from, to, operatorId, page, pageSize, ct);
        return new ResponsePersonalDifferencesJson
        {
            DateFrom = from, DateTo = to, AsOfDate = cutoff, CurrentShortageThreshold = threshold,
            AutomaticPayrollDeduction = false, UnassignedSourceCount = result.Totals.Where(t => t.OperatorId == null).Sum(t => t.Count), TotalCount = result.TotalCount, Page = page, PageSize = pageSize,
            People = result.Totals.Where(t => t.OperatorId != null).OrderBy(t => t.OperatorId).Select(t => new ResponsePersonalDifferenceTotalJson
            {
                OperatorId = t.OperatorId, OperatorName = t.OperatorName, OperatorActive = t.OperatorActive,
                Shortage = t.Shortage, Overage = t.Overage, SignedNet = t.Overage - t.Shortage, SourceCount = t.Count,
                ThresholdShortageBasis = t.ThresholdShortageBasis,
                CurrentThresholdAlert = applyCurrentThreshold && t.OperatorId != null && threshold != null && t.ThresholdShortageBasis >= threshold
            }).ToList(),
            Entries = result.Entries.Select(e => new ResponsePersonalDifferenceEntryJson
            {
                SourceId = e.SourceId, SourceKind = e.SourceKind, SessionId = e.SessionId, OperatorId = e.OperatorId,
                SourceDate = e.SourceDate, PostedOn = e.PostedOn, SourceSignedAmount = e.SourceSignedAmount, ShortageContribution = e.OperatorId == null ? 0m : e.Shortage,
                OverageContribution = e.OperatorId == null ? 0m : e.Overage,
                ThresholdShortageContribution = e.OperatorId == null ? 0m : e.ThresholdShortageContribution, ComparisonRevision = e.ComparisonRevision
            }).ToList()
        };
    }
}
