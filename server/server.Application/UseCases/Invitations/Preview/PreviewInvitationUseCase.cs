using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Invitations.Preview;

public sealed class PreviewInvitationUseCase(IOperatorsRepository operators, InvitationWorkflow workflow)
{
    public async Task<ResponseInvitationPreviewJson> Execute(RequestInvitationTokenJson request)
    {
        InvitationWorkflow.ValidateToken(request);
        var invitation = await workflow.FindCapability(request.Token);
        workflow.RequirePending(invitation);
        return new() { BranchName = invitation.Branch.Name, Role = invitation.Role, ExpiresAt = invitation.ExpiresAt,
            OperatorName = invitation.OperatorId.HasValue
                ? (await operators.GetActiveByIdAndBranchIdAsNoTracking(invitation.OperatorId.Value, invitation.BranchId))?.Name : null };
    }
}
