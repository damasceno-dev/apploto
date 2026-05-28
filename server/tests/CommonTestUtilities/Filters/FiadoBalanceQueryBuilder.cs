using server.Domain.Models;

namespace CommonTestUtilities.Filters;

public class FiadoBalanceQueryBuilder
{
    private Guid? _clientId;
    private DateTime _asOfDate = new DateTime(2025, 1, 31);

    public FiadoBalanceQueryBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public FiadoBalanceQueryBuilder WithAsOfDate(DateTime asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public FiadoBalanceQuery Build()
    {
        return new FiadoBalanceQuery
        {
            ClientId = _clientId,
            AsOfDate = _asOfDate
        };
    }
}
