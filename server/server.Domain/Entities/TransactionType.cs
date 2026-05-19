using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class TransactionType : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    public Guid CategoryId { get; init; }
    public Category Category { get; init; } = null!;
}
