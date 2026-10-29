using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.OpeningProposal;

/// <summary>
/// GetOpeningProposal (§6.20): the prefilled Conferir abertura e iniciar for a cashbox — source kind/id/revision,
/// missing-versus-zero state and whether a start is possible now. Read-only; never acknowledges or starts.
/// </summary>
public class GetSessionOpeningProposalUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IProductsRepository productsRepository,
    IDailyClosesRepository dailyClosesRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    SessionOpeningResolver openingResolver,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseSessionOpeningProposalJson> Execute(Guid accountId, CancellationToken ct = default)
    {
        if (accountId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, cashbox.Id);
        if (cashbox.Type != AccountType.Terminal)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);

        var proposal = await openingResolver.ProposeForNextSession(branchUser.BranchId, cashbox.Id, ct);
        var active = await dailyClosesRepository.GetActiveCustodyByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct);
        var pendingHandover = active is null
            ? null
            : await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(branchUser.BranchId, active.Id, ct);
        var unresolvedHistory = await dailyClosesRepository.HasUnresolvedHistoryByAccountIdAsNoTracking(
            branchUser.BranchId,
            cashbox.Id,
            ct);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        var products = await productsRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId, ct);
        var productsById = products.ToDictionary(product => product.Id);
        var productIds = products
            .Where(product => product.Id != cashVarianceProductId)
            .Select(product => product.Id)
            .Union(proposal.Values.Keys);

        return new ResponseSessionOpeningProposalJson
        {
            AccountId = cashbox.Id,
            AccountName = cashbox.Name,
            TerminalId = cashbox.TerminalId,
            SourceKind = proposal.SourceKind,
            SourceId = proposal.SourceId,
            SourceRevision = proposal.SourceRevision,
            SourceDate = proposal.SourceDate,
            IsMissing = proposal.IsMissing,
            ActiveSessionId = active?.Id,
            PendingHandoverId = pendingHandover?.Id,
            HasUnresolvedHistory = unresolvedHistory,
            CanStart = active is null && unresolvedHistory is false && cashbox.TerminalId is not null,
            Items = productIds
                .Select(productId => new ResponseSessionOpeningProposalItemJson
                {
                    ProductId = productId,
                    ProductName = productsById.TryGetValue(productId, out var product) ? product.Name : string.Empty,
                    DisplayOrder = product?.DisplayOrder ?? int.MaxValue,
                    Value = proposal.IsMissing ? null : proposal.Values.GetValueOrDefault(productId)
                })
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.ProductId)
                .ToList()
        };
    }
}
