namespace server.Domain.Entities.Enums;

/// <summary>
/// Which event date a receivable cutoff compares (M9.7 Phase 1.2). <c>Posted</c> is the financial basis and the
/// default, so a report as of a locked date is reproducible; <c>Occurred</c> is the operational history basis.
/// </summary>
public enum ReceivableCutoffBasis
{
    Posted = 1,
    Occurred = 2
}
