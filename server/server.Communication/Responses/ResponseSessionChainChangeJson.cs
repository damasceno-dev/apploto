namespace server.Communication.Responses;

/// <summary>
/// A declared or rearranged past session, with every session whose Sequence or opening dependency changed and the
/// cashbox rows that moved with it.
/// </summary>
public class ResponseSessionChainChangeJson
{
    public ResponseDailyCloseJson Session { get; init; } = null!;
    public List<Guid> MovedTransactionIds { get; init; } = [];
    public List<ResponseAffectedSessionJson> AffectedSessions { get; init; } = [];
}
