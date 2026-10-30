namespace server.Domain.Entities.Enums;

/// <summary>
/// Current manager attribution of an opening discrepancy. <see cref="Unattributed"/> is the initial state and is
/// distinct from <see cref="Neither"/>; only Outgoing/Incoming ever count toward a person.
/// </summary>
public enum OpeningDiscrepancyAttribution
{
    Unattributed = 0,
    Outgoing = 1,
    Incoming = 2,
    Neither = 3
}
