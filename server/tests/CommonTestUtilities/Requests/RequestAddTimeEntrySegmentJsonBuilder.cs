using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestAddTimeEntrySegmentJsonBuilder
{
    private DateTime _clockIn = DateTime.Today.AddHours(8);
    private DateTime? _clockOut = DateTime.Today.AddHours(17);

    public RequestAddTimeEntrySegmentJsonBuilder WithClockIn(DateTime clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public RequestAddTimeEntrySegmentJsonBuilder WithClockOut(DateTime? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public RequestAddTimeEntrySegmentJson Build()
    {
        return new RequestAddTimeEntrySegmentJson
        {
            ClockIn = _clockIn,
            ClockOut = _clockOut
        };
    }
}
