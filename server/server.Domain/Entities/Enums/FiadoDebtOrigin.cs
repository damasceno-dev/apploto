namespace server.Domain.Entities.Enums;

/// <summary>
/// Where a fiado debt comes from (M9.6 Phase 6, §6.19 O05/O19/O21). Only a new counter sale happens at the cashbox
/// and leaves expected cash unreceived; the other origins move nothing physical and never write a cashbox leg. The
/// non-counter origins are owned by their own later commands (M10 opening obligations and import, M9.7 cheque
/// conversion); no public route records them yet.
/// </summary>
public enum FiadoDebtOrigin
{
    CounterSale = 1,
    OpeningObligation = 2,
    ImportedDebt = 3,
    ChequeConversion = 4
}
