using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateTimeEntrySegmentJsonBuilder
{
    private DateTime _clockIn = DateTime.Today.AddHours(8);
    private DateTime? _clockOut = DateTime.Today.AddHours(17);

    public RequestUpdateTimeEntrySegmentJsonBuilder WithClockIn(DateTime clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public RequestUpdateTimeEntrySegmentJsonBuilder WithClockOut(DateTime? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public RequestUpdateTimeEntrySegmentJson Build()
    {
        return new RequestUpdateTimeEntrySegmentJson
        {
            ClockIn = _clockIn,
            ClockOut = _clockOut
        };
    }
}
