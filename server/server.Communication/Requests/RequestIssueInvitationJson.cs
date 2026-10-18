using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestIssueInvitationJson
{
    public string Email { get; init; } = string.Empty;
    public Role Role { get; init; } = Role.Member;
    public Guid? OperatorId { get; init; }
}
