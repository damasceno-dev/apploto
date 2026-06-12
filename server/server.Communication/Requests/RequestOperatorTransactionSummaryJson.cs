namespace server.Communication.Requests;

public class RequestOperatorTransactionSummaryJson
{
    public Guid? OperatorId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public bool Mine { get; set; }
}
