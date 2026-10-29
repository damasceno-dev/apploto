namespace server.Domain.Entities;

/// <summary>
/// The go-live counted value per product of one cashbox: the proposed opening of its first session. Each record
/// appends a new revision and retires the previous one; it carries no variance or close status.
/// </summary>
public class OpeningCount : EntityBase
{
    public uint Version { get; set; }

    public Guid BranchId { get; init; }

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public DateTime AsOfDate { get; init; }
    public int Revision { get; init; }
    public Guid RecordedByUserId { get; init; }

    public ICollection<OpeningCountItem> Items { get; init; } = [];
}
