using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestUpsertTimeEntryJsonBuilder
{
    private Guid _operatorId = Guid.NewGuid();
    private DateTime _date = DateTime.Today;
    private TimeEntryStatus _status = TimeEntryStatus.Present;
    private TimeEntryTapAction? _action;
    private List<RequestTimeEntrySegmentJson>? _segments = [];

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

    public RequestUpsertTimeEntryJsonBuilder WithAction(TimeEntryTapAction? action)
    {
        _action = action;
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithSegments(params RequestTimeEntrySegmentJson[] segments)
    {
        _segments = segments.ToList();
        return this;
    }

    public RequestUpsertTimeEntryJsonBuilder WithSegments(List<RequestTimeEntrySegmentJson>? segments)
    {
        _segments = segments;
        return this;
    }

    public RequestUpsertTimeEntryJson BuildMemberOpenTap()
    {
        _action = TimeEntryTapAction.Open;
        _segments = null;
        return Build();
    }

    public RequestUpsertTimeEntryJson BuildMemberCloseTap()
    {
        _action = TimeEntryTapAction.Close;
        _segments = null;
        return Build();
    }

    public RequestUpsertTimeEntryJson BuildAdminSnapshot(params RequestTimeEntrySegmentJson[] segments)
    {
        _action = null;
        _segments = segments.ToList();
        return Build();
    }

    public RequestUpsertTimeEntryJson Build()
    {
        return new RequestUpsertTimeEntryJson
        {
            OperatorId = _operatorId,
            Date = _date,
            Status = _status,
            Action = _action,
            Segments = _segments
        };
    }
}
