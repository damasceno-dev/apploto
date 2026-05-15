namespace server.Communication.Requests;

public class RequestTimeEntrySegmentJson
{
    public Guid? Id { get; init; }
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; init; }
}
