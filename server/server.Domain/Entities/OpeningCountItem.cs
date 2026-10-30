namespace server.Domain.Entities;

public class OpeningCountItem : EntityBase
{
    public Guid OpeningCountId { get; init; }
    public OpeningCount OpeningCount { get; init; } = null!;

    public Guid ProductId { get; init; }
    public decimal Value { get; init; }
}
