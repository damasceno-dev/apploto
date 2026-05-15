using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpsertTimeEntryJson
{
    public Guid OperatorId { get; init; }
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; init; }
    public TimeEntryTapAction? Action { get; init; }
    public List<RequestTimeEntrySegmentJson>? Segments { get; init; }
}
