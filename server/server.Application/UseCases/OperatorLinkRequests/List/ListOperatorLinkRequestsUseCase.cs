using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.OperatorLinkRequests.List;

public sealed class ListOperatorLinkRequestsUseCase(
    IAuthenticationService authentication,
    IOnboardingRepository onboarding,
    OperatorLinkRequestReader reader)
{
    public async Task<ResponseListOperatorLinkRequestsJson> Execute(RequestListOperatorLinkRequestsJson request)
    {
        SecurityValidation.Validate(new ListOperatorLinkRequestsFluentValidation(), request);
        var actor = await authentication.GetAuthorizedBranchUser(Role.Admin, Role.Manager);
        var (items, total) = await onboarding.ListRequests(actor.BranchId, request.Status, request.Page, request.PageSize);
        var responses = new List<ResponseOperatorLinkRequestJson>();
        foreach (var item in items) responses.Add(OperatorLinkRequestReader.Map(item, item.User.Name, await reader.State(item.UserId, item.BranchId)));
        return new() { Items = responses, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}
