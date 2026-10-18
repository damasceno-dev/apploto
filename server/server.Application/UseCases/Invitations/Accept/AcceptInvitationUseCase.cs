using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Invitations.Accept;

public sealed class AcceptInvitationUseCase(
    IAuthenticationService authentication,
    IUsersRepository users,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    InvitationWorkflow workflow)
{
    public async Task<ResponseAcceptInvitationJson> Execute(RequestInvitationTokenJson request)
    {
        InvitationWorkflow.ValidateToken(request);
        var identity = await authentication.GetAuthenticatedUser();
        var candidate = await workflow.FindCapability(request.Token);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(candidate.BranchId), SecurityRules.EmailKey(candidate.Email));
        var invitation = await workflow.FindCapability(request.Token);
        workflow.RequirePending(invitation);
        var user = await users.GetById(identity.Id) ?? throw InvitationWorkflow.InvalidToken();
        if (user.Email != invitation.Email)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.INVITATION_EMAIL_MISMATCH);
        var membership = await workflow.Join(invitation, user);
        await unitOfWork.Commit();
        await scope.Complete();
        return new() { BranchId = invitation.BranchId, BranchUserId = membership.Id, Role = membership.Role, OperatorId = invitation.OperatorId };
    }
}
