using server.Domain.Models;

namespace CommonTestUtilities.Filters;

public class OpenChequeAgingListFilterBuilder
{
    private Guid? _accountId;
    private Guid? _clientId;
    private DateTime _asOfDate = new DateTime(2025, 5, 28);
    private int _page = 1;
    private int _pageSize = 50;

    public OpenChequeAgingListFilterBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public OpenChequeAgingListFilterBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public OpenChequeAgingListFilterBuilder WithAsOfDate(DateTime asOfDate)
    {
        _asOfDate = asOfDate;
        return this;
    }

    public OpenChequeAgingListFilterBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public OpenChequeAgingListFilterBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public OpenChequeAgingListFilter Build()
    {
        return new OpenChequeAgingListFilter
        {
            AccountId = _accountId,
            ClientId = _clientId,
            AsOfDate = _asOfDate,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
