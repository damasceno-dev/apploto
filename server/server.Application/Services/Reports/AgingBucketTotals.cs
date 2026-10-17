using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;

namespace server.Application.Services.Reports;

/// <summary>
/// Folds whole-set due-date totals into one entry per <see cref="AgingBucket"/> (declared order, zeros included),
/// with each due date bucketed at the report's as-of date by <see cref="ReportAgingBucketizer"/>.
/// </summary>
public static class AgingBucketTotals
{
    public static IReadOnlyList<ResponseAgingBucketTotalJson> Build(IEnumerable<AgingDueDateTotalRow> rows, DateTime asOfDate)
    {
        var byBucket = rows
            .GroupBy(row => ReportAgingBucketizer.BucketFor(row.DueDate, asOfDate))
            .ToDictionary(group => group.Key, group => (Value: group.Sum(row => row.Value), RowCount: group.Sum(row => row.RowCount)));

        return Enum.GetValues<AgingBucket>()
            .Select(bucket => new ResponseAgingBucketTotalJson
            {
                Bucket = bucket,
                Value = byBucket.TryGetValue(bucket, out var total) ? total.Value : 0m,
                RowCount = byBucket.TryGetValue(bucket, out var count) ? count.RowCount : 0
            })
            .ToList();
    }
}
