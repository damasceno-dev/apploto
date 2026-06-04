namespace server.Communication.Responses;

public class ResponseEditTransactionPreviewJson
{
    public Guid TransactionId { get; init; }
    public ResponseTransactionEditImpactJson Impact { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
