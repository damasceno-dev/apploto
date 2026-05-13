namespace server.Domain.Entities;

public class TimeEntrySegment : EntityBase
{
    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }

    public Guid TimeEntryId { get; init; }
    public TimeEntry TimeEntry { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
