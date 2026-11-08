using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestPayItemJson : RequestPeopleEvidenceJson
{
    public Guid OperatorId { get; set; }
    public Guid CategoryId { get; set; }
    public DateTime SourceMonth { get; set; }
    public DateTime SourceDate { get; set; }
    public decimal Amount { get; set; }
    public PayItemKind Kind { get; set; }
    public string? DeductionPolicy { get; set; }
    public bool Cancelled { get; set; }
    public string? CommissionCalculationId { get; set; }
}
