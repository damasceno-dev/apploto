using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseListTransactionsJson
{
    public IReadOnlyList<ResponseListTransactionItemJson> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
}

public class ResponseListTransactionItemJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; init; }
    public Direction Direction { get; init; }
    public TransactionStatus Status { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid TransactionTypeId { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public Guid? OriginTransactionId { get; init; }
    public DateTime CreatedAt { get; init; }
}
