namespace server.Communication.Responses;

public class ResponseSessionOpeningProposalItemJson
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public decimal? Value { get; init; }
}
