using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Immutable delta captured atomically when a session becomes official; source and posting dates remain distinct.</summary>
public class EmployeeCommissionFeePosting : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid OperatorId { get; init; }
    public Guid SessionId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid TransactionId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public CommissionBasisComponent Component { get; init; }
    public decimal SignedAmount { get; init; }
    /// <summary>Transaction classification revision causing this publication; pre-upgrade records remain unknown.</summary>
    public int? ClassificationRevision { get; init; }
    public ClassificationProvenance? ClassificationProvenance { get; init; }
}
