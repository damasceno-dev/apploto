using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseListDailyClosesJson
{
    public IReadOnlyList<ResponseListDailyCloseItemJson> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
}

public class ResponseListDailyCloseItemJson
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
