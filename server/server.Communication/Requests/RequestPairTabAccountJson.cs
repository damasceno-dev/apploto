namespace server.Communication.Requests;

public class RequestPairTabAccountJson
{
    public Guid TerminalAccountId { get; init; }
    public Guid TabAccountId { get; init; }
}
