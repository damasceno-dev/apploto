namespace server.Communication.Responses;

public class ResponseInstallmentPreviewJson
{
    public decimal TotalValue { get; init; }
    public int InstallmentCount { get; init; }
    public IReadOnlyList<ResponseInstallmentPreviewRowJson> Rows { get; init; } = [];

    /// <summary>
    /// Downstream impact forecast for the would-be plan (open-cheque aging, fiado balance, cash
    /// variance). Additive and always present.
    /// </summary>
    public ResponseInstallmentPreviewImpactJson Impact { get; init; } = new();
}
