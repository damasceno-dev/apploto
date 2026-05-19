namespace server.Domain.Entities;

public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
