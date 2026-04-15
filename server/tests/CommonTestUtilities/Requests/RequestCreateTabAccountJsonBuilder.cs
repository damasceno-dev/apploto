using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateTabAccountJsonBuilder
{
    private Guid _terminalAccountId = Guid.NewGuid();

    public RequestCreateTabAccountJsonBuilder WithTerminalAccountId(Guid terminalAccountId)
    {
        _terminalAccountId = terminalAccountId;
        return this;
    }

    public RequestCreateTabAccountJson Build()
    {
        return new RequestCreateTabAccountJson
        {
            TerminalAccountId = _terminalAccountId
        };
    }
}
