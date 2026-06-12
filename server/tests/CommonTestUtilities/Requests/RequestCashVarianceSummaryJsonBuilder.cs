using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCashVarianceSummaryJsonBuilder
{
    private Guid? _accountId;
    private DateTime _dateFrom = new DateTime(2025, 1, 1);
    private DateTime _dateTo = new DateTime(2025, 1, 31);
    private int _page = 1;
    private int _pageSize = 50;

    public RequestCashVarianceSummaryJsonBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestCashVarianceSummaryJsonBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestCashVarianceSummaryJsonBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestCashVarianceSummaryJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestCashVarianceSummaryJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestCashVarianceSummaryJson Build()
    {
        return new RequestCashVarianceSummaryJson
        {
            AccountId = _accountId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
