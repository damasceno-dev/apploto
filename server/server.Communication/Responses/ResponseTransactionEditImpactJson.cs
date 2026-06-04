namespace server.Communication.Responses;

public class ResponseTransactionEditImpactJson
{
    public ResponseReceivableImpactJson ReceivableImpact { get; init; } = new();
    public ResponseFiadoBalanceImpactJson FiadoBalanceImpact { get; init; } = new();
    public ResponseCashVarianceImpactJson CashVarianceImpact { get; init; } = new();
}
