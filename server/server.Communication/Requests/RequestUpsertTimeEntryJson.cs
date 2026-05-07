using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpsertTimeEntryJson
{
    public Guid OperatorId { get; set; }
    public DateTime Date { get; set; }
    public TimeEntryStatus Status { get; set; }
    public TimeOnly? ClockIn { get; set; }
    public TimeOnly? ClockOut { get; set; }
}
