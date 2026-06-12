namespace server.Communication.Responses;

public class ResponseOperatorCategoryTotalJson
{
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
}
