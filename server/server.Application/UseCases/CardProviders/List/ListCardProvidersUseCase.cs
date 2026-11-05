using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.CardProviders.List;

public class ListCardProvidersUseCase(IAuthenticationService authentication, ICardSettlementsRepository cards)
{
    public async Task<IReadOnlyList<ResponseCardProviderJson>> Execute(CancellationToken ct = default)
    {
        var actor = await authentication.GetAuthorizedBranchUser(Role.Manager, Role.Admin);
        return (await cards.ListProvidersAsNoTracking(actor.BranchId, ct)).Select(p => p.ToResponse()).ToList();
    }
}
