namespace server.Domain.Entities;

public class GoLiveImport : EntityBase
{
    public DateTime? SourceLockDate { get; set; }
    public Guid BranchId { get; init; }
    public Guid UploadedByUserId { get; init; }
    public string SourceDigest { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "access-csv-1";
    public string Profile { get; init; } = string.Empty;
    public string SourceJson { get; init; } = string.Empty;
    public string MappingJson { get; set; } = "[]";
    public int MappingRevision { get; set; }
    public string? PreviewJson { get; set; }
    public Guid? PreviewId { get; set; }
    public string? TargetFingerprint { get; set; }
    public string Status { get; set; } = "Uploaded";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UpdatedByUserId { get; set; }
    public string? ResultJson { get; set; }
}
