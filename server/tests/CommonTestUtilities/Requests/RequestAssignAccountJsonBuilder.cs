using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestAssignAccountJsonBuilder
{
    private Guid _accountId = Guid.NewGuid();

    public RequestAssignAccountJsonBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestAssignAccountJson Build()
    {
        return new RequestAssignAccountJson
        {
            AccountId = _accountId
        };
    }
}
