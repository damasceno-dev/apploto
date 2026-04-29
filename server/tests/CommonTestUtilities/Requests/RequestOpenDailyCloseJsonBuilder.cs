using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestOpenDailyCloseJsonBuilder
{
    private DateTime _date = DateTime.Today;
    private Guid _accountId = Guid.NewGuid();

    public RequestOpenDailyCloseJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestOpenDailyCloseJsonBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestOpenDailyCloseJson Build()
    {
        return new RequestOpenDailyCloseJson
        {
            Date = _date,
            AccountId = _accountId
        };
    }
}
