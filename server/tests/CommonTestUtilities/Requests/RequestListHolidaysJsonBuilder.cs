using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestListHolidaysJsonBuilder
{
    private int? _year;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private int _page = 1;
    private int _pageSize = 20;

    public RequestListHolidaysJsonBuilder WithYear(int? year)
    {
        _year = year;
        return this;
    }

    public RequestListHolidaysJsonBuilder WithDateFrom(DateTime? dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestListHolidaysJsonBuilder WithDateTo(DateTime? dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestListHolidaysJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestListHolidaysJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestListHolidaysJson Build()
    {
        return new RequestListHolidaysJson
        {
            Year = _year,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
