namespace server.Communication.Responses;

public class ResponseHolidayJson
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string? Description { get; init; }
    public Guid BranchId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool Active { get; init; }
}
