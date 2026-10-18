using server.Communication.Requests;
using server.Application.Services.Security;
using server.Application.UseCases.BranchUsers;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Invitations.Reissue;

public sealed class ReissueInvitationUseCase(
    IAuthenticationService authentication,
    IUsersRepository users,
    IBranchUsersRepository memberships,
    IOnboardingRepository onboarding,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    AccountSecurityOptions options,
    InvitationWorkflow workflow)
{
    public async Task<ResponseInvitationJson> Execute(Guid id, RequestReissueInvitationJson? request = null)
    {
        if (request is not null) SecurityValidation.Validate(new ReissueInvitationFluentValidation(), request);
        var actor = await authentication.GetAuthenticatedBranchUser();
        var candidate = await onboarding.GetInvitation(id, actor.BranchId) ?? throw InvitationWorkflow.NotFound();
        var token = SecurityRules.NewToken();
        Invitation invitation;
        await using (var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(actor.BranchId), SecurityRules.EmailKey(candidate.Email)))
        {
            actor = await SecurityRules.FreshActor(memberships, actor);
            invitation = await onboarding.GetInvitation(id, actor.BranchId) ?? throw InvitationWorkflow.NotFound();
            if (!BranchUserPermissionRules.CanAssignRole(actor.Role, invitation.Role)) throw InvitationWorkflow.NotFound();
            if (invitation.Status == InvitationStatus.Accepted)
                throw new ConflictException(ResourcesErrorMessages.INVITATION_ALREADY_ACCEPTED);
            var other = await onboarding.GetPendingInvitation(actor.BranchId, invitation.Email);
            if (other is not null && other.Id != id)
                throw new ConflictException(ResourcesErrorMessages.INVITATION_PENDING_CONFLICT);
            var user = await users.GetByEmail(invitation.Email);
            if (user is { Active: false }) throw new ConflictException(ResourcesErrorMessages.INVITATION_TARGET_UNAVAILABLE);
            if (user is not null && (await memberships.GetByUserIdAndBranchId(user.Id, actor.BranchId))?.Active == true)
                throw new ConflictException(ResourcesErrorMessages.INVITATION_MEMBERSHIP_EXISTS);
            var operatorId = request is null ? invitation.OperatorId : request.OperatorId;
            if (operatorId.HasValue && invitation.Role != Role.Member)
                throw new OnValidationException([ResourcesErrorMessages.INVITATION_OPERATOR_ROLE_INVALID]);
            await workflow.RequireAvailableOperator(operatorId, actor.BranchId);
            invitation.OperatorId = operatorId;
            invitation.TokenHash = SecurityRules.Hash(token);
            invitation.ExpiresAt = workflow.Now().Add(options.InvitationLifetime);
            invitation.Status = InvitationStatus.Pending;
            invitation.RespondedAt = null;
            invitation.DeliveryStatus = MailDeliveryStatus.Pending;
            await unitOfWork.Commit();
            await scope.Complete();
        }
        return await workflow.Deliver(invitation, token);
    }
}
