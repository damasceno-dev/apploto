namespace server.Communication.Responses;

public class ResponseInstallmentPreviewJson
{
    public decimal TotalValue { get; init; }
    public int InstallmentCount { get; init; }
    public IReadOnlyList<ResponseInstallmentPreviewRowJson> Rows { get; init; } = [];
}
