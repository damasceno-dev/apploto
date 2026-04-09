using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Category : EntityBase
{
    public string Name { get; init; } = string.Empty;
    public Direction DefaultDirection { get; init; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public ICollection<TransactionType> TransactionTypes { get; init; } = [];
}
