namespace server.Domain.Entities.Enums;

/// <summary>Where a settlement event came from (M9.7 Phase 1.4).</summary>
public enum SettlementEventProvenance
{
    /// <summary>Appended by an explicit, authenticated command.</summary>
    Command = 1,

    /// <summary>Reconstructed by the upgrade from a legacy <c>PaidAt</c>; it has no actor and keeps the original instant.</summary>
    MigratedFromPaidAt = 2
}
