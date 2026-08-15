using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponsePutDailyCloseItemsJson
{
    public ResponseDailyCloseJson DailyClose { get; init; } = null!;
    public ResponseAffectedDailyCloseSuccessorJson? AffectedSuccessor { get; init; }
}

public class ResponseAffectedDailyCloseSuccessorJson
{
    public Guid DailyCloseId { get; init; }
    public DateTime Date { get; init; }
    public DailyCloseStatus PreviousStatus { get; init; }
    public DailyCloseStatus NewStatus { get; init; }
    public DateTime OpeningRecheckRequiredAt { get; init; }
}
