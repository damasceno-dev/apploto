namespace server.Domain.Entities;

/// <summary>Per-product snapshot of a count attempt: the counted value and the opening value it was compared with.</summary>
public class SessionCountAttemptItem : EntityBase
{
    public Guid AttemptId { get; init; }
    public SessionCountAttempt Attempt { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;

    public decimal? OpeningValue { get; init; }
    public decimal? CountedValue { get; init; }
}
