namespace server.Communication.Requests;

public class RequestUpdateTimeEntrySegmentJson
{
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; init; }
}
