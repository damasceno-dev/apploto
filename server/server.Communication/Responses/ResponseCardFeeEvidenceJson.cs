namespace server.Communication.Responses;

public class ResponseCardFeeEvidenceJson
{
    public Guid Id { get; init; }
    public int Version { get; init; }
    public decimal ConfirmedFee { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public string Basis { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
    public DateTime PostedOn { get; init; }
    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
