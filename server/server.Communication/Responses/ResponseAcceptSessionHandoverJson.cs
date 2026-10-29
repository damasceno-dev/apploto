namespace server.Communication.Responses;

public class ResponseAcceptSessionHandoverJson
{
    public ResponseSessionHandoverJson Handover { get; init; } = null!;
    public ResponseDailyCloseJson OutgoingSession { get; init; } = null!;
    public ResponseStartSessionJson Incoming { get; init; } = null!;
}
