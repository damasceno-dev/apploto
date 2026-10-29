using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

/// <summary>
/// Conferir abertura e iniciar (§6.20): either a start acknowledgment of the proposal read from
/// <c>GET /session/opening-proposal</c> (source kind/id/revision echoed back, the full acknowledged vector, explicit
/// zero when nothing is counted) or the acceptance of a pending handover (<see cref="HandoverId"/> and its version).
/// </summary>
public class RequestSessionOpeningAcknowledgmentJson
{
    /// <summary>The employee answerable for the session; omitted by a Member, who always starts their own.</summary>
    public Guid? CustodianOperatorId { get; init; }

    /// <summary>The proposal's source kind; null when the proposal is missing.</summary>
    public SessionOpeningSourceKind? SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public long? SourceRevision { get; init; }

    /// <summary>True only when the operator explicitly confirms that the cashbox opens with nothing.</summary>
    public bool ExplicitZero { get; init; }

    public List<RequestSessionOpeningItemJson>? Items { get; init; }

    /// <summary>Required when a manager starts for another operator; optional note for a differing opening.</summary>
    public string? Reason { get; init; }

    public Guid? HandoverId { get; init; }
    public uint? HandoverVersion { get; init; }
}
