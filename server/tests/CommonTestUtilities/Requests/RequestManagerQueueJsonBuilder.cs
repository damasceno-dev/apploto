using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestManagerQueueJsonBuilder
{
    private DateTime _date = new(2025, 1, 15);

    public RequestManagerQueueJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestManagerQueueJson Build()
    {
        return new RequestManagerQueueJson
        {
            Date = _date
        };
    }
}
