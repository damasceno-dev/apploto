namespace server.Communication.Requests;

public class RequestCardSettlementJson
{
    public Guid ProviderId { get; init; }
    public uint ProviderVersion { get; init; }
    public string ExternalIdentity { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
    public DateTime? PostedOn { get; init; }
    public DateTime PlanningBasisOn { get; init; }
    public decimal? ConfirmedFee { get; init; }
    public bool ManagerConfirmedFee { get; init; }
    public string? FeeBasis { get; init; }
    public string? FeeEvidence { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public IReadOnlyList<RequestCardSettlementItemJson> Items { get; init; } = [];
}

public class RequestCardSettlementItemJson
{
    public Guid TransactionId { get; init; }
    public uint TransactionVersion { get; init; }
    public int ExpectedVersion { get; init; }
    public decimal GrossAmount { get; init; }
}
