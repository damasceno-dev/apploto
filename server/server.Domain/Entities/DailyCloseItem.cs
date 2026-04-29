namespace server.Domain.Entities;

public class DailyCloseItem : EntityBase
{
    public decimal Value { get; set; }

    public Guid DailyCloseId { get; init; }
    public DailyClose DailyClose { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;
}
