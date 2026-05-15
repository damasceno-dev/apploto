using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseTimeEntryJson
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; init; }
    public decimal TotalHours { get; init; }
    public decimal BalanceHours { get; init; }
    public bool IsInProgress { get; init; }
    public List<ResponseTimeEntrySegmentJson> Segments { get; init; } = [];
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public bool Active { get; init; }
}
