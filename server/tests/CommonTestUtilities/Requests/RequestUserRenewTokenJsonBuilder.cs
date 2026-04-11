using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUserRenewTokenJsonBuilder
{
    private string _value = Guid.NewGuid().ToString("N");

    public RequestUserRenewTokenJsonBuilder WithValue(string value)
    {
        _value = value;
        return this;
    }

    public RequestUserRenewTokenJson Build()
    {
        return new RequestUserRenewTokenJson
        {
            Value = _value
        };
    }
}
