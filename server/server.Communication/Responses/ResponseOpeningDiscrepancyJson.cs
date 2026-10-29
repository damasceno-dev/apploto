using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOpeningDiscrepancyJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public Guid SessionId { get; init; }
    public Guid SessionOpeningId { get; init; }
    public Guid? HandoverId { get; init; }
    public Guid? PredecessorSessionId { get; init; }
    public SessionOpeningSourceKind SourceKind { get; init; }
    public int CurrentRevision { get; init; }
    public decimal CurrentTotalDelta { get; init; }
    public OpeningDiscrepancyAttribution Attribution { get; init; }
    public int? AttributedRevision { get; init; }

    /// <summary>True only for an explicit Outgoing/Incoming attribution of the current revision.</summary>
    public bool CountsTowardPerson { get; init; }
    public List<OpeningDiscrepancyAttribution> AllowedTargets { get; init; } = [];
    public List<ResponseOpeningDiscrepancyItemJson> Items { get; init; } = [];
}
