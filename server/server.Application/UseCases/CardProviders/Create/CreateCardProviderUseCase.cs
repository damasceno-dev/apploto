using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Application.UseCases.CardProviders;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.CardProviders.Create;

public class CreateCardProviderUseCase(
    BankCommandRunner runner,
    ICardSettlementsRepository cards,
    IAccountsRepository accounts,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseCardProviderJson> Execute(RequestCardProviderJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new CardProviderFluentValidation(), request);
        return runner.Run<RequestCardProviderJson, ResponseCardProviderJson>("POST /card-provider", request, key,
            (_, _) => Task.FromResult<IReadOnlyCollection<Guid>>([request.BankAccountId]),
            async (actor, token) =>
            {
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(request.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                await cards.LockProviderIdentity(actor.BranchId, request.ExternalIdentity.Trim(), token);
                if (await cards.ProviderIdentityExists(actor.BranchId, request.ExternalIdentity.Trim(), token)) throw new ConflictException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
                var provider = new CardProvider
                {
                    BranchId = actor.BranchId, Name = request.Name.Trim(), ExternalIdentity = request.ExternalIdentity.Trim(),
                    BankAccountId = bank.Id, Brand = request.Brand.Trim(), DelayDays = request.DelayDays,
                    BusinessDays = request.BusinessDays, Instrument = request.Instrument, CreatedByUserId = actor.UserId
                };
                await cards.AddProvider(provider, token);
                await unitOfWork.Commit(token);
                return (provider.Id, provider.ToResponse());
            }, ct);
    }

}
