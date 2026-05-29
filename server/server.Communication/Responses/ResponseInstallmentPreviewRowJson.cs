namespace server.Communication.Responses;

public class ResponseInstallmentPreviewRowJson
{
    public int Index { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
    public string Description { get; init; } = string.Empty;
}
