namespace server.Application.Services.Transactions;

public sealed class BranchClock : IBranchClock
{
    private static readonly TimeZoneInfo BranchTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTime LocalBusinessDate(DateTime utcInstant)
    {
        // Replace the MVP fixed business timezone with branch-level timezone configuration. (to do)
        var normalizedUtcInstant = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtcInstant, BranchTimeZone).Date;
    }

    public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
    {
        return localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }
}
