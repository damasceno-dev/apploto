using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestUpsertTimeEntryJsonBuilder
{
    private Guid _operatorId = Guid.NewGuid();
    private DateTime _date = DateTime.Today;
    private TimeEntryStatus _status = TimeEntryStatus.Present;
    private TimeOnly? _clockIn = new TimeOnly(8, 0);
    private TimeOnly? _clockOut = new TimeOnly(17, 0);

    public RequestUpsertTimeEntryJsonBuilder WithOperatorId(Guid operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithStatus(TimeEntryStatus status)
    {
        _status = status;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithClockIn(TimeOnly? clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithClockOut(TimeOnly? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithNoClocks()
    {
        _clockIn = null;
        _clockOut = null;
        return this;
    }

    public RequestUpsertTimeEntryJson Build()
    {
        return new RequestUpsertTimeEntryJson
        {
            OperatorId = _operatorId,
            Date = _date,
            Status = _status,
            ClockIn = _clockIn,
            ClockOut = _clockOut
        };
    }
}
