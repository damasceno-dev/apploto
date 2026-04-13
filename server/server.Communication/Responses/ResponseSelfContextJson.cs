namespace server.Communication.Responses;

public class ResponseSelfContextJson
{
    public Guid? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public ResponseOperatorAccountJson? PrimaryAccount { get; set; }
    public List<ResponseOperatorAccountJson> AvailableAccounts { get; set; } = [];
}
