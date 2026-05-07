namespace server.Application.Services.Transactions;

public interface IBranchClock
{
    DateTime UtcNow();
    DateTime LocalBusinessDateTime(DateTime utcInstant);
    DateTime LocalBusinessDate(DateTime utcInstant);
    bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant);
}
