namespace server.Domain.Entities;

public class OperatorAccount : EntityBase
{
    public Guid OperatorId { get; init; }
    public Operator Operator { get; init; } = null!;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public bool IsPrimary { get; set; } = false;
}
