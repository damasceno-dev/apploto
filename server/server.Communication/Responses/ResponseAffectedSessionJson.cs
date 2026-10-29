using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseAffectedSessionJson
{
    public Guid SessionId { get; init; }
    public uint Version { get; init; }
    public int? Sequence { get; init; }
    public DailyCloseStatus Status { get; init; }
    public bool OpeningRecheckRequired { get; init; }
    public bool DiscrepancyRevised { get; init; }
}
