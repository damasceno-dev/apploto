using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Holiday : EntityBase
{
    public DateTime Date { get; init; }
    public string? Description { get; set; }
    public HolidaySource Source { get; set; } = HolidaySource.Manual;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
