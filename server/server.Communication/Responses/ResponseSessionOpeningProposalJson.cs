using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseSessionOpeningProposalJson
{
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? TerminalId { get; init; }

    /// <summary>Null when the cashbox has no source: only an explicit zero acknowledgment can start it.</summary>
    public SessionOpeningSourceKind? SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public long? SourceRevision { get; init; }
    public DateTime? SourceDate { get; init; }
    public bool IsMissing { get; init; }

    public Guid? ActiveSessionId { get; init; }
    public Guid? PendingHandoverId { get; init; }
    public bool HasUnresolvedHistory { get; init; }
    public bool CanStart { get; init; }

    public List<ResponseSessionOpeningProposalItemJson> Items { get; init; } = [];
}
