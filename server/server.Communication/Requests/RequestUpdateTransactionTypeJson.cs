using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpdateTransactionTypeJson
{
    public string Name { get; init; } = string.Empty;
    public SettlementRule SettlementRule { get; init; }
    public bool RequiresTabAccountAndClient { get; init; }
}
