namespace server.Domain.Entities;

public class Product : EntityBase
{
    public string Name { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
