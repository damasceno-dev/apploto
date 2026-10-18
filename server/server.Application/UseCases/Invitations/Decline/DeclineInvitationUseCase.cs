using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Invitations.Decline;

public sealed class DeclineInvitationUseCase(
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    InvitationWorkflow workflow)
{
    public async Task Execute(RequestInvitationTokenJson request)
    {
        InvitationWorkflow.ValidateToken(request);
        var candidate = await workflow.FindCapability(request.Token);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(candidate.BranchId), SecurityRules.EmailKey(candidate.Email));
        var invitation = await workflow.FindCapability(request.Token);
        if (invitation.ExpiresAt <= workflow.Now()) throw InvitationWorkflow.InvalidToken();
        if (invitation.Status == InvitationStatus.Declined) { await scope.Complete(); return; }
        workflow.RequirePending(invitation);
        invitation.Status = InvitationStatus.Declined;
        invitation.RespondedAt = workflow.Now();
        await unitOfWork.Commit();
        await scope.Complete();
    }
}
