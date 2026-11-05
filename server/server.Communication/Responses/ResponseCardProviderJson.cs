using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCardProviderJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ExternalIdentity { get; init; } = string.Empty;
    public Guid BankAccountId { get; init; }
    public SettlementInstrument Instrument { get; init; } = SettlementInstrument.CreditCard;
    public string Brand { get; init; } = string.Empty;
    public int DelayDays { get; init; }
    public bool BusinessDays { get; init; }
}
