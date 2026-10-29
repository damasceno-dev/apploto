namespace server.Domain.Entities;

public class SessionHandoverItem : EntityBase
{
    public Guid SessionHandoverId { get; init; }
    public SessionHandover SessionHandover { get; init; } = null!;

    public Guid ProductId { get; init; }
    public decimal Value { get; init; }
}
