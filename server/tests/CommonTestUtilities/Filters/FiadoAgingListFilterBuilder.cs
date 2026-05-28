using server.Domain.Models;

namespace CommonTestUtilities.Filters;

public class FiadoAgingListFilterBuilder
{
    private Guid? _clientId;
    private Guid? _accountId;
    private DateTime _asOfDate = new DateTime(2025, 5, 28);
    private int _page = 1;
    private int _pageSize = 50;

    public FiadoAgingListFilterBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public FiadoAgingListFilterBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public FiadoAgingListFilterBuilder WithAsOfDate(DateTime asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public FiadoAgingListFilterBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public FiadoAgingListFilterBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public FiadoAgingListFilter Build()
    {
        return new FiadoAgingListFilter
        {
            ClientId = _clientId,
            AccountId = _accountId,
            AsOfDate = _asOfDate,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
