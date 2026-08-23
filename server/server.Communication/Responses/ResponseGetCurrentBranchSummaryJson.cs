namespace server.Communication.Responses;

public class ResponseGetCurrentBranchSummaryJson
{
    public ResponseBranchSummaryJson Branch { get; set; } = null!;
    public DateOnly BranchLocalDate { get; set; }
    public DateTime BranchLocalDateTime { get; set; }
}
