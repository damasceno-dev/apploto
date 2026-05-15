using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestTimeEntrySegmentJsonBuilder
{
    private Guid? _id;
    private DateTime _clockIn = DateTime.Today.AddHours(8);
    private DateTime? _clockOut = DateTime.Today.AddHours(17);

    public RequestTimeEntrySegmentJsonBuilder WithId(Guid? id)
    {
        _id = id;
        return this;
    }

    public RequestTimeEntrySegmentJsonBuilder WithClockIn(DateTime clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public RequestTimeEntrySegmentJsonBuilder WithClockOut(DateTime? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public RequestTimeEntrySegmentJson Build()
    {
        return new RequestTimeEntrySegmentJson
        {
            Id = _id,
            ClockIn = _clockIn,
            ClockOut = _clockOut
        };
    }
}
