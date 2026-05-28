using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestFiadoAgingJsonBuilder
{
    private Guid? _clientId;
    private Guid? _accountId;
    private DateTime? _asOfDate;
    private int _page = 1;
    private int _pageSize = 50;

    public RequestFiadoAgingJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestFiadoAgingJsonBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestFiadoAgingJsonBuilder WithAsOfDate(DateTime? asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public RequestFiadoAgingJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestFiadoAgingJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestFiadoAgingJson Build()
    {
        return new RequestFiadoAgingJson
        {
            ClientId = _clientId,
            AccountId = _accountId,
            AsOfDate = _asOfDate,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
