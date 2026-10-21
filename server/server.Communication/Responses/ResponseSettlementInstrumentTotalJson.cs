using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseSettlementInstrumentTotalJson
{
    public SettlementInstrument Instrument { get; init; }
    public int Count { get; init; }
    public decimal Value { get; init; }
}
