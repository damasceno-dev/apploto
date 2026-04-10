namespace server.Communication.Responses;

public class ResponseCreateBranchSessionJson
{
    public string Token { get; set; } = string.Empty;
    public ResponseBranchSummaryJson Branch { get; set; } = null!;
}
