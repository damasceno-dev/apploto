namespace server.Communication.Responses;

public class ResponseOpeningDiscrepancyItemJson
{
    public Guid ProductId { get; init; }
    public decimal ProposedValue { get; init; }
    public decimal AcknowledgedValue { get; init; }
    public decimal Delta { get; init; }
}
