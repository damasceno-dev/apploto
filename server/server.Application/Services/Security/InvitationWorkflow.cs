using server.Application.UseCases.BranchUsers;
using server.Application.UseCases.Invitations;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Security;

public sealed class InvitationWorkflow(
    IBranchUsersRepository memberships,
    IOperatorsRepository operators,
    IOnboardingRepository onboarding,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    AccountSecurityOptions options,
    ISecurityMailSender mail)
{
    public async Task<BranchUser> Join(Invitation invitation, User user)
    {
        var membership = await memberships.GetByUserIdAndBranchId(user.Id, invitation.BranchId);
        if (membership?.Active == true) throw new ConflictException(ResourcesErrorMessages.INVITATION_MEMBERSHIP_EXISTS);
        var op = await RequireAvailableOperator(invitation.OperatorId, invitation.BranchId);
        if (op is not null && await operators.ExistsActiveLinkedByUserIdAndBranchId(user.Id, invitation.BranchId))
            throw new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        if (membership is null)
        {
            membership = new BranchUser { BranchId = invitation.BranchId, UserId = user.Id, Role = invitation.Role, CreatedAt = Now() };
            await memberships.Add(membership);
        }
        else { membership.Active = true; membership.Role = invitation.Role; }
        if (op is not null) op.UserId = user.Id;
        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedByUserId = user.Id;
        invitation.RespondedAt = Now();
        return membership;
    }

    public async Task<Operator?> RequireAvailableOperator(Guid? id, Guid branchId)
    {
        if (!id.HasValue) return null;
        var op = await operators.GetActiveByIdAndBranchId(id.Value, branchId);
        if (op is null || op.UserId.HasValue)
            throw new ConflictException(ResourcesErrorMessages.INVITATION_OPERATOR_UNAVAILABLE);
        return op;
    }

    public async Task<ResponseInvitationJson> Deliver(Invitation invitation, string token)
    {
        var hash = SecurityRules.Hash(token);
        var sent = await mail.Send(invitation.Email, "Convite para a lotérica", "Para responder ao convite, abra: " + options.Link("invitation", token));
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(invitation.BranchId), SecurityRules.EmailKey(invitation.Email));
        var current = await onboarding.GetInvitation(invitation.Id, invitation.BranchId) ?? throw NotFound();
        // A concurrent reissue may have replaced this attempt; never overwrite its result.
        if (current.TokenHash == hash)
        {
            current.DeliveryStatus = sent ? MailDeliveryStatus.Sent : MailDeliveryStatus.Failed;
            await unitOfWork.Commit();
        }
        await scope.Complete();
        return Map(current);
    }
    public async Task<Invitation> FindCapability(string token) => await onboarding.GetInvitationByHash(SecurityRules.Hash(token)) ?? throw InvalidToken();
    public void RequirePending(Invitation invitation)
    {
        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= Now()) throw InvalidToken();
    }
    public static void ValidateToken(RequestInvitationTokenJson request) => SecurityValidation.Validate(new InvitationTokenFluentValidation(), request);
    public static void EnsureRole(BranchUser actor, Role role)
    {
        if (!BranchUserPermissionRules.CanAssignRole(actor.Role, role)) throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }
    public TimeSpan InvitationLifetime => options.InvitationLifetime;
    public DateTime Now() => time.GetUtcNow().UtcDateTime;
    public static ConflictException InvalidToken() => new(ResourcesErrorMessages.SECURITY_TOKEN_INVALID);
    public static NotFoundException NotFound() => new(ResourcesErrorMessages.INVITATION_NOT_FOUND);
    public ResponseInvitationJson Map(Invitation i, DateTime? snapshotUtc = null) => new()
    {
        Id = i.Id, Email = i.Email, Role = i.Role, OperatorId = i.OperatorId,
        Status = i.Status == InvitationStatus.Pending && i.ExpiresAt <= (snapshotUtc ?? Now()) ? InvitationStatus.Expired : i.Status,
        ExpiresAt = i.ExpiresAt, DeliveryStatus = i.DeliveryStatus
    };

}
