namespace server.Domain.Models;

public class OperatorTransactionSummaryQuery
{
    public Guid OperatorId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public IReadOnlyList<Guid>? AllowedAccountIds { get; set; }
}
