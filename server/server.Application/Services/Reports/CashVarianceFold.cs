using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;

namespace server.Application.Services.Reports;

public sealed record CashVarianceFigures(
    int Count,
    decimal Total,
    decimal Mean,
    decimal Max,
    decimal Min,
    int SubmittedCount,
    int ApprovedCount,
    int RejectedCount);

/// <summary>
/// The cash-variance summary arithmetic over official (non-Draft) variance values, shared by
/// <c>GET /report/cash-variance</c> (which already holds the rows it pages) and the management overview and monthly
/// financial totals (which fold per status in the database). Both inputs reach one formula.
/// </summary>
public static class CashVarianceFold
{
    public static CashVarianceFigures From(IReadOnlyCollection<VarianceTimeSeriesRow> rows)
    {
        return FromStatusTotals(rows
            .GroupBy(row => row.DailyCloseStatus)
            .Select(group => new OfficialVarianceStatusTotalRow(
                group.Key,
                group.Count(),
                group.Sum(row => row.Value),
                group.Max(row => row.Value),
                group.Min(row => row.Value)))
            .ToList());
    }

    public static CashVarianceFigures FromStatusTotals(IReadOnlyCollection<OfficialVarianceStatusTotalRow> totals)
    {
        var count = totals.Sum(row => row.Count);
        var total = totals.Sum(row => row.Total);
        return new CashVarianceFigures(
            count,
            total,
            count > 0 ? total / count : 0m,
            count > 0 ? totals.Max(row => row.Max) : 0m,
            count > 0 ? totals.Min(row => row.Min) : 0m,
            CountOf(totals, DailyCloseStatus.Submitted),
            CountOf(totals, DailyCloseStatus.Approved),
            CountOf(totals, DailyCloseStatus.Rejected));
    }

    private static int CountOf(IEnumerable<OfficialVarianceStatusTotalRow> totals, DailyCloseStatus status) =>
        totals.Where(row => row.Status == status).Sum(row => row.Count);
}
