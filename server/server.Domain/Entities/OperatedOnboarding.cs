namespace server.Domain.Entities;

public sealed class OperatedOnboarding : EntityBase
{
    public Guid RequestId { get; init; }
    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
    public Guid PlatformUserId { get; init; }
    public Guid OwnerInvitationId { get; init; }
    public Invitation OwnerInvitation { get; init; } = null!;
    public string RequestHash { get; init; } = string.Empty;
}
