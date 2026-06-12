namespace server.Communication.Requests;

public class RequestCashVarianceSummaryJson
{
    public Guid? AccountId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
