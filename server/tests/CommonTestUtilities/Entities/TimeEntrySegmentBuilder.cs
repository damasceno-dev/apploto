using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class TimeEntrySegmentBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _timeEntryId = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _clockIn = DateTime.Today.AddHours(8);
    private DateTime? _clockOut = DateTime.Today.AddHours(17);
    private bool _active = true;
    private DateTime? _updatedAt;
    private Guid? _updatedByUserId;
    private TimeEntry? _timeEntry;

    public TimeEntrySegmentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TimeEntrySegmentBuilder WithTimeEntryId(Guid timeEntryId)
    {
        _timeEntryId = timeEntryId;
        _timeEntry = null;
        return this;
    }

    public TimeEntrySegmentBuilder WithTimeEntry(TimeEntry timeEntry)
    {
        _timeEntry = timeEntry;
        _timeEntryId = timeEntry.Id;
        return this;
    }

    public TimeEntrySegmentBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TimeEntrySegmentBuilder WithClockIn(DateTime clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public TimeEntrySegmentBuilder WithClockOut(DateTime? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public TimeEntrySegmentBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public TimeEntrySegmentBuilder WithUpdated(DateTime updatedAt, Guid updatedByUserId)
    {
        _updatedAt = updatedAt;
        _updatedByUserId = updatedByUserId;
        return this;
    }

    public TimeEntrySegment Build()
    {
        return new TimeEntrySegment
        {
            Id = _id,
            CreatedAt = _createdAt,
            TimeEntryId = _timeEntryId,
            TimeEntry = _timeEntry!,
            ClockIn = _clockIn,
            ClockOut = _clockOut,
            Active = _active,
            UpdatedAt = _updatedAt,
            UpdatedByUserId = _updatedByUserId
        };
    }
}
