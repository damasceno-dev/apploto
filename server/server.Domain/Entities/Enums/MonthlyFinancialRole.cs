namespace server.Domain.Entities.Enums;

/// <summary>
/// Which figure of the monthly financial report a transaction type's rows feed (M11 Phase 6): explicit admin
/// configuration by type identity, never inferred from the name. Read at query time, so rename and retirement keep it.
/// </summary>
public enum MonthlyFinancialRole
{
    /// <summary>Not a monthly financial figure; the default for every type until configured.</summary>
    None = 0,

    /// <summary>The day's bolão service fee (working answer SQ3): an <c>In</c> type, summed per Terminal/day.</summary>
    BolaoServiceFee = 1,

    /// <summary>Unsold bolão quotas / Federal fractions the shop kept (working answer SQ4): an <c>Out</c> commercial loss.</summary>
    CommercialLeftover = 2
}
