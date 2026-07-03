using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestDashboardJsonBuilder
{
    private DateTime _date = new(2025, 1, 15);

    public RequestDashboardJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestDashboardJson Build()
    {
        return new RequestDashboardJson
        {
            Date = _date
        };
    }
}
