namespace server.Communication.Requests;

public class RequestEmployeeStatementJson
{
    public Guid OperatorId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
