namespace server.Communication.Responses;

public class ResponseTimeEntrySegmentJson
{
    public Guid Id { get; init; }
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public bool Active { get; init; }
}
