using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Custody work of the branch, independent of the selected day (§6.21 Work Queue): sessions in custody, unresolved
/// handover offers, opening discrepancies still Unattributed and unresolved legacy history. Derived from persisted
/// records; never local arithmetic.
/// </summary>
public class ResponseManagerQueueCustodyJson
{
    public List<ResponseManagerQueueOpenSessionJson> OpenSessions { get; init; } = [];
    public List<ResponseSessionHandoverJson> PendingHandovers { get; init; } = [];
    /// <summary>
    /// Nonzero opening discrepancies whose current revision needs a manager attribution: never attributed, or
    /// attributed on an earlier revision a correction superseded (the earlier decision no longer counts toward a person).
    /// </summary>
    public List<ResponseManagerQueueOpeningDiscrepancyJson> OpeningDiscrepanciesAwaitingAttribution { get; init; } = [];
    public int UnresolvedMappingIssueCount { get; init; }
}

public class ResponseManagerQueueOpenSessionJson
{
    public Guid SessionId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public int? Sequence { get; init; }
    public Guid? CustodianOperatorId { get; init; }
    public string? CustodianOperatorName { get; init; }
    public DateTime? StartedAt { get; init; }
    public DailyCloseStatus Status { get; init; }

    /// <summary>True when the custody started on an earlier business day and needs an explicit finish.</summary>
    public bool StartedBeforeToday { get; init; }
}

public class ResponseManagerQueueOpeningDiscrepancyJson
{
    public Guid OpeningDiscrepancyId { get; init; }
    public Guid SessionId { get; init; }
    public Guid SessionOpeningId { get; init; }
    public Guid? HandoverId { get; init; }
    public Guid? PredecessorSessionId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string? CustodianOperatorName { get; init; }
    public int CurrentRevision { get; init; }
    public decimal CurrentTotalDelta { get; init; }
    public OpeningDiscrepancyAttribution Attribution { get; init; }
    public int? AttributedRevision { get; init; }
    public uint Version { get; init; }
}
