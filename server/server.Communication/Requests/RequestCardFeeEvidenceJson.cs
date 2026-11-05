namespace server.Communication.Requests;

public class RequestCardFeeEvidenceJson
{
    public Guid BatchId { get; init; }
    public uint ReceiptVersion { get; init; }
    public int ExpectedFeeEvidenceVersion { get; init; }
    public required decimal ConfirmedFee { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public string Basis { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
    public DateTime? PostedOn { get; init; }
}
