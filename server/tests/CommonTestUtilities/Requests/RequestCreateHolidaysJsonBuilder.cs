using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateHolidaysJsonBuilder
{
    private List<RequestCreateHolidayJson> _holidays =
    [
        new RequestCreateHolidayJson { Date = new DateTime(2025, 9, 7), Description = "Independência do Brasil" }
    ];

    public RequestCreateHolidaysJsonBuilder WithHolidays(List<RequestCreateHolidayJson> holidays)
    {
        _holidays = holidays;
        return this;
    }

    public RequestCreateHolidaysJsonBuilder WithSingleHoliday(DateTime date, string? description = null)
    {
        _holidays = [new RequestCreateHolidayJson { Date = date, Description = description }];
        return this;
    }

    public RequestCreateHolidaysJsonBuilder WithEmptyList()
    {
        _holidays = [];
        return this;
    }

    public RequestCreateHolidaysJson Build()
    {
        return new RequestCreateHolidaysJson { Holidays = _holidays };
    }
}
