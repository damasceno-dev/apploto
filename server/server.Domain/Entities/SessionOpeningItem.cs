namespace server.Domain.Entities;

public class SessionOpeningItem : EntityBase
{
    public Guid SessionOpeningId { get; init; }
    public SessionOpening SessionOpening { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;

    /// <summary>Null when the proposal had no value for the product (missing is not zero).</summary>
    public decimal? ProposedValue { get; init; }
    public decimal AcknowledgedValue { get; init; }
}
