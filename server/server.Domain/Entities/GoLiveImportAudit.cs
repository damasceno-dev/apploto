namespace server.Domain.Entities;

public class GoLiveImportAudit : EntityBase
{
    public Guid GoLiveImportId { get; init; }
    public Guid ActorUserId { get; init; }
    public int MappingRevision { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EvidenceJson { get; init; } = string.Empty;
}

public class GoLiveImportTarget : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid GoLiveImportId { get; init; }
    public string SourceTable { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public Guid TargetId { get; init; }
    public string TargetKind { get; init; } = string.Empty;
}
