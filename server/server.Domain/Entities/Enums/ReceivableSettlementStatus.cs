namespace server.Domain.Entities.Enums;

/// <summary>A receivable's status at a cutoff, derived from its events (never stored).</summary>
public enum ReceivableSettlementStatus
{
    Open = 1,
    PartiallySettled = 2,
    Settled = 3
}
