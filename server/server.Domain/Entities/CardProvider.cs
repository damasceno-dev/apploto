using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Branch-owned manual provider configuration. No assumed acquirer rate or calendar.</summary>
public class CardProvider : EntityBase
{
    public Guid BranchId { get; init; }
    public uint Version { get; set; }
    public string Name { get; init; } = string.Empty;
    public string ExternalIdentity { get; init; } = string.Empty;
    public Guid BankAccountId { get; init; }
    public SettlementInstrument Instrument { get; init; } = SettlementInstrument.CreditCard;
    public string Brand { get; init; } = string.Empty;
    public int DelayDays { get; init; }
    public bool BusinessDays { get; init; }
    public Guid CreatedByUserId { get; init; }
}
