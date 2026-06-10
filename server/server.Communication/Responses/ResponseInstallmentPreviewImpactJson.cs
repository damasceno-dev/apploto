namespace server.Communication.Responses;

/// <summary>
/// Downstream impact forecast for a would-be cheque installment plan, returned alongside the existing
/// row preview on <c>POST /transaction/installment/preview</c>. Each section is always present and
/// reads "empty" when it does not apply: <see cref="OpenChequeAgingImpact"/> is the single would-be
/// open-cheque group, <see cref="FiadoBalanceImpact"/> is the optional aggregated client delta (Tab
/// account + client only), and <see cref="CashVarianceImpact"/> is the ledger-side variance shift on
/// the plan's <c>(account, date)</c>. The fiado and cash-variance sections reuse the create/edit
/// preview DTOs verbatim; the open-cheque section is a group rollup, so it has its own envelope.
/// </summary>
public class ResponseInstallmentPreviewImpactJson
{
    public ResponseOpenChequeAgingImpactJson OpenChequeAgingImpact { get; init; } = new();
    public ResponseFiadoBalanceImpactJson FiadoBalanceImpact { get; init; } = new();
    public ResponseCashVarianceImpactJson CashVarianceImpact { get; init; } = new();
}
