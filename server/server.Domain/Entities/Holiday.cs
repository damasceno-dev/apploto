namespace server.Domain.Entities;

public class Holiday : EntityBase
{
    public DateTime Date { get; init; }
    public string? Description { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
