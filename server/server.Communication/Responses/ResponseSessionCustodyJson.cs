using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>Custody identity of a session, returned beside its independent review status (§6.20).</summary>
public class ResponseSessionCustodyJson
{
    public SessionMappingState MappingState { get; init; }
    public SessionOrigin Origin { get; init; }
    public SessionCustodyState? CustodyState { get; init; }
    public int? Sequence { get; init; }
    public Guid? CustodianOperatorId { get; init; }
    public string? CustodianOperatorName { get; init; }
    public Guid? TerminalId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public bool OpeningAcknowledged { get; init; }
    public bool CustodyHistoryComplete { get; init; }
}
