namespace server.Domain.Entities.Enums;

/// <summary>How a posted row's frozen classification snapshot was obtained (M9.7 Phase 1.3).</summary>
public enum ClassificationProvenance
{
    /// <summary>Captured from the type when the row was posted (created Active or finalized).</summary>
    Posting = 1,

    /// <summary>
    /// Copied from the type's configuration at the M9.7 upgrade. It equals what the query-time reports showed then,
    /// but it is not evidence of the classification in force when the row was originally posted.
    /// </summary>
    MigratedFromTypeAtUpgrade = 2,

    /// <summary>Changed by an explicit, audited open-period reclassification.</summary>
    OpenPeriodReclassification = 3
}
