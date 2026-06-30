namespace server.Communication.Requests;

public class RequestTimeEntryBalanceSummaryJson
{
    public Guid? OperatorId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public bool Mine { get; set; }
}
