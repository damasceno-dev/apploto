using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestPairTabAccountJsonBuilder
{
    private Guid _terminalAccountId = Guid.NewGuid();
    private Guid _tabAccountId = Guid.NewGuid();

    public RequestPairTabAccountJsonBuilder WithTerminalAccountId(Guid terminalAccountId)
    {
        _terminalAccountId = terminalAccountId;
        return this;
    }

    public RequestPairTabAccountJsonBuilder WithTabAccountId(Guid tabAccountId)
    {
        _tabAccountId = tabAccountId;
        return this;
    }

    public RequestPairTabAccountJson Build()
    {
        return new RequestPairTabAccountJson
        {
            TerminalAccountId = _terminalAccountId,
            TabAccountId = _tabAccountId
        };
    }
}
