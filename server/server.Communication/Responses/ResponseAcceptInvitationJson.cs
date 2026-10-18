using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseAcceptInvitationJson
{
    public Guid BranchId { get; init; }
    public Guid BranchUserId { get; init; }
    public Role Role { get; init; }
    public Guid? OperatorId { get; init; }
}
