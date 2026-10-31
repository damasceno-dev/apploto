namespace server.Domain.Entities.Enums;

public enum SessionFieldEffectOrigin
{
    /// <summary>A count save changed the field.</summary>
    Save = 0,

    /// <summary>The M9.6 Phase 7 migration adopted the session's single ordinary fee/report row as the field's effect.</summary>
    MigratedLegacyRow = 1
}
