namespace server.Communication.Responses;

public class ResponseResolveCustodyExceptionJson
{
    public ResponseDailyCloseJson OutgoingSession { get; init; } = null!;
    public ResponseStartSessionJson? Incoming { get; init; }
}
