namespace server.Domain.Entities.Enums;

public enum SessionApprovalMode
{
    /// <summary>The approver is neither recorder, submitter nor custodian of the session.</summary>
    Independent = 0,

    /// <summary>Aprovar como único gestor: allowed only with exactly one active Manager/Admin, with a reason.</summary>
    SingleActiveManager = 1
}
