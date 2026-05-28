using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestFiadoBalanceJsonBuilder
{
    private Guid? _clientId;
    private DateTime? _asOfDate;

    public RequestFiadoBalanceJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestFiadoBalanceJsonBuilder WithAsOfDate(DateTime? asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public RequestFiadoBalanceJson Build()
    {
        return new RequestFiadoBalanceJson
        {
            ClientId = _clientId,
            AsOfDate = _asOfDate
        };
    }
}
