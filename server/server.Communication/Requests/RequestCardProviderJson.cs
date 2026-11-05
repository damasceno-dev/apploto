using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCardProviderJson
{
    public string Name { get; init; } = string.Empty;
    public string ExternalIdentity { get; init; } = string.Empty;
    public Guid BankAccountId { get; init; }
    public SettlementInstrument Instrument { get; init; } = SettlementInstrument.CreditCard;
    public string Brand { get; init; } = string.Empty;
    public required int DelayDays { get; init; }
    public required bool BusinessDays { get; init; }
}
