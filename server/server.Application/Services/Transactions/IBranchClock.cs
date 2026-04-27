namespace server.Application.Services.Transactions;

public interface IBranchClock
{
    DateTime UtcNow();
    DateTime LocalBusinessDate(DateTime utcInstant);
    bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant);
}
