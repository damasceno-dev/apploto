namespace server.Domain.Entities;

/// <summary>
/// One product's opening value before and after an opening-change event. A null value means no
/// saved item for the product, which is distinct from a recorded zero.
/// </summary>
public class DailyCloseReviewEventItem : EntityBase
{
    public Guid DailyCloseReviewEventId { get; init; }
    public DailyCloseReviewEvent DailyCloseReviewEvent { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;
    public string ProductName { get; init; } = string.Empty;

    public decimal? BeforeValue { get; init; }
    public decimal? AfterValue { get; init; }
}
