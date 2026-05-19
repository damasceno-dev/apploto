using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseTransactionTypeJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
}
