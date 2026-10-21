namespace server.Domain.Entities.Enums;

/// <summary>
/// The state of one management-overview section (M11 Phase 7): <c>Empty</c> means its source ran and found nothing;
/// <c>NotConfigured</c> means a deterministic configuration is missing, so the section must never be read as zero.
/// </summary>
public enum OverviewSectionStatus
{
    Ready = 0,
    Empty = 1,
    NotConfigured = 2
}
