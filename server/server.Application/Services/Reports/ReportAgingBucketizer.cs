using server.Domain.Entities.Enums;

namespace server.Application.Services.Reports;

public static class ReportAgingBucketizer
{
    public static AgingBucket BucketFor(DateTime dueDate, DateTime asOfDate)
    {
        if (dueDate.Date > asOfDate.Date)
            return AgingBucket.Current;

        var daysOutstanding = (int)(asOfDate.Date - dueDate.Date).TotalDays;

        return daysOutstanding switch
        {
            <= 30 => AgingBucket.Days0To30,
            <= 60 => AgingBucket.Days31To60,
            <= 90 => AgingBucket.Days61To90,
            _ => AgingBucket.Days91Plus
        };
    }
}
