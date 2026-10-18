using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Invitations.List;

public sealed class ListInvitationsUseCase(
    IAuthenticationService authentication,
    IOnboardingRepository onboarding,
    InvitationWorkflow workflow)
{
    public async Task<ResponseListInvitationsJson> Execute(RequestListInvitationsJson request)
    {
        SecurityValidation.Validate(new ListInvitationsFluentValidation(), request);
        var actor = await authentication.GetAuthorizedBranchUser(Role.Admin, Role.Manager);
        var snapshotUtc = workflow.Now();
        var (items, total) = await onboarding.ListInvitations(actor.BranchId, actor.Role == Role.Admin,
            request.Status, request.Page, request.PageSize, snapshotUtc);
        return new() { Items = items.Select(item => workflow.Map(item, snapshotUtc)).ToList(), TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}
