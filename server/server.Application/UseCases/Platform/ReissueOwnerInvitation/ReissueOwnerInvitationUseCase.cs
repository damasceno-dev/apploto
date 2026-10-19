using server.Application.Services.Security;
using server.Application.UseCases.Platform.OperateBranch;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Platform.ReissueOwnerInvitation;

public sealed class ReissueOwnerInvitationUseCase(IAuthenticationService authentication, IAccountSecurityRepository security,
    IUsersRepository users, IOperatedOnboardingRepository records, IOnboardingRepository invitations,
    ISecurityCoordination coordination, IUnitOfWork unitOfWork, InvitationWorkflow workflow)
{
    public async Task<ResponseOperatedOnboardingJson> Execute(Guid requestId)
    {
        var actor = await authentication.GetAuthenticatedUser();
        var candidate = await records.Get(requestId) ?? throw new NotFoundException(ResourcesErrorMessages.ONBOARDING_NOT_FOUND);
        var token = SecurityRules.NewToken();
        await using (var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(actor.Email),
            SecurityRules.EmailKey(candidate.OwnerInvitation.Email), SecurityRules.BranchKey(candidate.BranchId)))
        {
            var fresh = await SecurityRules.RequireUser(security, actor.Id);
            if (!fresh.IsPlatformOperator) throw new TokenWithoutPermissionException(ResourcesErrorMessages.PLATFORM_ONLY);
            candidate = await records.Get(requestId) ?? throw new NotFoundException(ResourcesErrorMessages.ONBOARDING_NOT_FOUND);
            var invite = await invitations.GetInvitation(candidate.OwnerInvitationId, candidate.BranchId)
                ?? throw new NotFoundException(ResourcesErrorMessages.ONBOARDING_NOT_FOUND);
            if (invite.Status == InvitationStatus.Accepted)
                throw new ConflictException(ResourcesErrorMessages.INVITATION_ALREADY_ACCEPTED);
            if (invite.Role != Role.Admin || invite.OperatorId.HasValue)
                throw new ConflictException(ResourcesErrorMessages.ONBOARDING_REQUEST_CONFLICT);
            var owner = await users.GetByEmail(invite.Email);
            if (owner is { Active: false }) throw new ConflictException(ResourcesErrorMessages.INVITATION_TARGET_UNAVAILABLE);
            invite.TokenHash = SecurityRules.Hash(token);
            invite.ExpiresAt = workflow.Now().Add(workflow.InvitationLifetime);
            invite.Status = InvitationStatus.Pending;
            invite.RespondedAt = null;
            invite.DeliveryStatus = MailDeliveryStatus.Pending;
            await unitOfWork.Commit();
            await scope.Complete();
        }
        candidate = await records.Get(requestId) ?? throw new NotFoundException(ResourcesErrorMessages.ONBOARDING_NOT_FOUND);
        await workflow.Deliver(candidate.OwnerInvitation, token);
        return OperateBranchUseCase.Map(candidate, workflow.Now());
    }
}
