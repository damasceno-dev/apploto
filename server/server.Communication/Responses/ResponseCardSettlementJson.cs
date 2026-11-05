namespace server.Communication.Responses;

public class ResponseCardSettlementJson
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public string ExternalIdentity { get; init; } = string.Empty;
    public DateTime PlannedArrivalOn { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal? ConfirmedFee { get; init; }
    public decimal? ExpectedNetAmount { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public decimal? UnmatchedDifference { get; init; }
    public bool FeeUnknown { get; init; }
    public ResponseBankReceiptJson BankReceipt { get; init; } = null!;
    public IReadOnlyList<ResponseReceivableJson> Receivables { get; init; } = [];
}
