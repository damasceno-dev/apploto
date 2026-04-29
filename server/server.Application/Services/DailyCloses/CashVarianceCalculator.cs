namespace server.Application.Services.DailyCloses;

public class CashVarianceCalculator : ICashVarianceCalculator
{
    public Task<decimal> CalculateAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        Guid currentDailyCloseId,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
