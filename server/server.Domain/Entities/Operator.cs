namespace server.Domain.Entities;

public class Operator : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }
}
