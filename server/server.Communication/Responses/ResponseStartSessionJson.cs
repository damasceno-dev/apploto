namespace server.Communication.Responses;

public class ResponseStartSessionJson
{
    public ResponseDailyCloseJson Session { get; init; } = null!;
    public Guid SessionOpeningId { get; init; }
    public ResponseOpeningDiscrepancyJson? OpeningDiscrepancy { get; init; }
}
