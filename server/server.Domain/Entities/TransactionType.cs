namespace server.Domain.Entities;

public class TransactionType : EntityBase
{
    public string Name { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }
    public Category Category { get; init; } = null!;
}
