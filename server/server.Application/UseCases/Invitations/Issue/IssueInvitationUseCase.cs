using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Invitations.Issue;

public sealed class IssueInvitationUseCase(
    IAuthenticationService authentication,
    IUsersRepository users,
    IBranchUsersRepository memberships,
    IOnboardingRepository onboarding,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    AccountSecurityOptions options,
    InvitationWorkflow workflow)
{
    public async Task<ResponseInvitationJson> Execute(RequestIssueInvitationJson request)
    {
        SecurityValidation.Validate(new IssueInvitationFluentValidation(), request);
        if (request.OperatorId.HasValue && request.Role != Role.Member)
            throw new OnValidationException([ResourcesErrorMessages.INVITATION_OPERATOR_ROLE_INVALID]);
        var actor = await authentication.GetAuthenticatedBranchUser();
        var email = await users.NormalizeEmail(request.Email);
        var token = SecurityRules.NewToken();
        Invitation invitation;
        await using (var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(actor.BranchId), SecurityRules.EmailKey(email)))
        {
            actor = await SecurityRules.FreshActor(memberships, actor);
            InvitationWorkflow.EnsureRole(actor, request.Role);
            var user = await users.GetByEmail(email);
            if (user is { Active: false }) throw new ConflictException(ResourcesErrorMessages.INVITATION_TARGET_UNAVAILABLE);
            if (user is not null && (await memberships.GetByUserIdAndBranchId(user.Id, actor.BranchId))?.Active == true)
                throw new ConflictException(ResourcesErrorMessages.INVITATION_MEMBERSHIP_EXISTS);
            var previous = await onboarding.GetPendingInvitation(actor.BranchId, email);
            var now = time.GetUtcNow().UtcDateTime;
            if (previous is not null)
            {
                if (previous.ExpiresAt > now) throw new ConflictException(ResourcesErrorMessages.INVITATION_PENDING_CONFLICT);
                previous.Status = InvitationStatus.Expired;
                await unitOfWork.Commit(); // Retire the filtered unique slot inside this same transaction before insertion.
            }
            await workflow.RequireAvailableOperator(request.OperatorId, actor.BranchId);
            invitation = new Invitation
            {
                BranchId = actor.BranchId, Email = email, Role = request.Role, OperatorId = request.OperatorId,
                IssuedByUserId = actor.UserId, TokenHash = SecurityRules.Hash(token), CreatedAt = now,
                ExpiresAt = now.Add(options.InvitationLifetime)
            };
            await onboarding.AddInvitation(invitation);
            await unitOfWork.Commit();
            await scope.Complete();
        }
        return await workflow.Deliver(invitation, token);
    }
}
