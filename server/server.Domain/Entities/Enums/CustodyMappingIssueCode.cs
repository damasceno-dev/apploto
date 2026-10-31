namespace server.Domain.Entities.Enums;

public enum CustodyMappingIssueCode
{
    LegacySessionUnmapped = 0,
    OrphanCashboxActivity = 1,

    /// <summary>
    /// M9.6 Phase 7: a session holds several ordinary rows (or a Draft row) of one close-field type, so no single row can
    /// become the field's effect; the rows stay as they are and the field refuses saves until they are resolved.
    /// </summary>
    AmbiguousSessionFieldRows = 2
}
