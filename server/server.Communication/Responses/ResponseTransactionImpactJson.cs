namespace server.Communication.Responses;

/// <summary>
/// Neutral impact envelope shared by both transaction previews — the edit-impact preview
/// (<c>POST /transaction/{id}/edit-preview</c>) and the create-impact preview
/// (<c>POST /transaction/preview</c>). Each section is always present and reads "empty" when
/// it does not apply to the previewed change.
/// </summary>
public class ResponseTransactionImpactJson
{
    public ResponseReceivableImpactJson ReceivableImpact { get; init; } = new();
    public ResponseFiadoBalanceImpactJson FiadoBalanceImpact { get; init; } = new();
    public ResponseCashVarianceImpactJson CashVarianceImpact { get; init; } = new();
}
