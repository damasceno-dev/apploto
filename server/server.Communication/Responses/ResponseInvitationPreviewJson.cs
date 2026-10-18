using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseInvitationPreviewJson
{
    public string BranchName { get; init; } = string.Empty;
    public Role Role { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string? OperatorName { get; init; }
}
