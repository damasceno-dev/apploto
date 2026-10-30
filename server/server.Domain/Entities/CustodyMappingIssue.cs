using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class CustodyMappingIssue : EntityBase
{
    public Guid BranchId { get; init; }
    public CustodyMappingSourceKind SourceKind { get; init; }
    public Guid SourceId { get; init; }
    public CustodyMappingIssueCode IssueCode { get; init; }
    public string SourceDigest { get; init; } = string.Empty;
    public CustodyMappingIssueStatus Status { get; set; } = CustodyMappingIssueStatus.Unresolved;
    public string? ResolutionReason { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
