namespace server.Domain.Entities;

public class Branch : EntityBase
{
    public string Name { get; init; } = string.Empty;
    public string? Cnpj { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }

    public ICollection<BranchUser> BranchUsers { get; init; } = [];
    public ICollection<Category> Categories { get; init; } = [];
    public ICollection<Product> Products { get; init; } = [];
    public Setting? Setting { get; init; }
}
