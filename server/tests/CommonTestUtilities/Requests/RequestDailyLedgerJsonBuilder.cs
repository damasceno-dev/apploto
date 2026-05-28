using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestDailyLedgerJsonBuilder
{
    private Guid _accountId = Guid.NewGuid();
    private DateTime _dateFrom = new DateTime(2025, 1, 1);
    private DateTime _dateTo = new DateTime(2025, 1, 31);
    private int _page = 1;
    private int _pageSize = 50;

    public RequestDailyLedgerJsonBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestDailyLedgerJsonBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestDailyLedgerJsonBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestDailyLedgerJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestDailyLedgerJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestDailyLedgerJson Build()
    {
        return new RequestDailyLedgerJson
        {
            AccountId = _accountId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
