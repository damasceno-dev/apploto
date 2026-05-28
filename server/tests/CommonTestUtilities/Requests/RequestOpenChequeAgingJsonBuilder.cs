using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestOpenChequeAgingJsonBuilder
{
    private Guid? _accountId;
    private Guid? _clientId;
    private DateTime? _asOfDate;
    private int _page = 1;
    private int _pageSize = 50;

    public RequestOpenChequeAgingJsonBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestOpenChequeAgingJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestOpenChequeAgingJsonBuilder WithAsOfDate(DateTime? asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public RequestOpenChequeAgingJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestOpenChequeAgingJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestOpenChequeAgingJson Build()
    {
        return new RequestOpenChequeAgingJson
        {
            AccountId = _accountId,
            ClientId = _clientId,
            AsOfDate = _asOfDate,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
