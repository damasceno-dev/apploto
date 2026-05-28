using server.Domain.Models;

namespace CommonTestUtilities.Filters;

public class DailyLedgerListFilterBuilder
{
    private Guid _accountId = Guid.NewGuid();
    private DateTime _dateFrom = new DateTime(2025, 1, 1);
    private DateTime _dateTo = new DateTime(2025, 1, 31);
    private int _page = 1;
    private int _pageSize = 50;

    public DailyLedgerListFilterBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public DailyLedgerListFilterBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public DailyLedgerListFilterBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public DailyLedgerListFilterBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public DailyLedgerListFilterBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public DailyLedgerListFilter Build()
    {
        return new DailyLedgerListFilter
        {
            AccountId = _accountId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
