using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class BranchUser : EntityBase
{
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public Role Role { get; init; } = Role.Member;
}
