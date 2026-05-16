namespace server.Communication.Requests;

public class RequestAddTimeEntrySegmentJson
{
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; init; }
}
