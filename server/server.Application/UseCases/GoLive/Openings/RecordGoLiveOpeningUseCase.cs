using server.Application.Services.Receivables;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Openings;

public class RecordGoLiveOpeningUseCase(
    BankCommandRunner runner,
    IGoLiveOpeningsRepository openings,
    IAccountsRepository accounts,
    IClientsRepository clients,
    IOperatorsRepository operators,
    ITransactionTypesRepository types,
    ITransactionsRepository transactions,
    LockDateGuard locks,
    IBranchClock clock,
    IBankingReadLocks readLocks,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseGoLiveOpeningJson> Execute(RequestGoLiveOpeningJson request, string? key, CancellationToken ct = default)
    {
        var validation = new GoLiveOpeningFluentValidation().Validate(request);
        if (!validation.IsValid)
            throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());
        return runner.Run<RequestGoLiveOpeningJson, ResponseGoLiveOpeningJson>(
            "POST /go-live/openings", request, key,
            (actor, token) => Task.FromResult<IReadOnlyCollection<Guid>>([actor.BranchId, request.AccountId, request.ClientId ?? request.AccountId]),
            (actor, token) => Stage(actor, request, token), ct);
    }

    public async Task<(Guid ResourceId, ResponseGoLiveOpeningJson Response)> Stage(BranchUser actor, RequestGoLiveOpeningJson request, CancellationToken token, Guid? previousId = null)
    {
        var (account, type, identity) = await Validate(actor, request, token);
        var opening = new GoLiveOpening
        {
            BranchId = actor.BranchId,
            AccountId = account.Id,
            ClientId = request.ClientId,
            IsCheque = request.IsCheque,
            Identity = identity,
            AsOfDate = request.AsOfDate,
            SourceEvidence = request.SourceEvidence.Trim(),
            SourceReference = request.SourceReference.Trim(),
            PreviousId = previousId,
            RecordedByUserId = actor.UserId
        };
        if (!opening.IsCheque && opening.ClientId is { } openingClient)
        {
            opening.ClientCreditTransactionId = await openings.GetClientCredit(actor.BranchId, openingClient, token);
            if (opening.ClientCreditTransactionId is not null) opening.ClientCreditObservedAt = clock.UtcNow();
        }
        await openings.Add(opening, token);
        var ids = new List<Guid>();
        Guid? originId = request.Items.Count > 1 ? Guid.NewGuid() : null;
        foreach (var item in request.Items)
        {
            var row = new Transaction
            {
                Id = ids.Count == 0 && originId is { } rootId ? rootId : Guid.NewGuid(),
                BranchId = actor.BranchId,
                AccountId = account.Id,
                ClientId = request.ClientId,
                GoLiveOpeningId = opening.Id,
                Date = request.AsOfDate,
                DueDate = item.DueDate,
                Value = item.Value,
                TransactionTypeId = type.Id,
                CategoryId = type.CategoryId,
                Direction = Direction.Out,
                RecordedByOperatorId = request.RecordedByOperatorId,
                CreatedByUserId = actor.UserId,
                Description = request.IsCheque ? "Cheque de implantação" : "Saldo fiado de implantação",
                OriginTransactionId = originId
            };
            // Match the shipped plan contract: first actual receivable is its own origin; no extra claim.
            await transactions.Add(row, token);
            ids.Add(row.Id);
        }
        await unitOfWork.Commit(token);
        var savedRows = await openings.Rows(actor.BranchId, opening.Id, token);
        return (opening.Id, new ResponseGoLiveOpeningJson
        {
            Id = opening.Id,
            Version = opening.Version,
            AccountId = opening.AccountId,
            ClientId = opening.ClientId,
            IsCheque = opening.IsCheque,
            AsOfDate = opening.AsOfDate,
            SourceReference = opening.SourceReference,
            SourceEvidence = opening.SourceEvidence,
            PreviousId = opening.PreviousId,
            Active = true,
            TransactionIds = ids,
            RecordedByOperatorId = request.RecordedByOperatorId,
            ClientCreditObserved = opening.ClientCreditObservedAt is not null,
            Items = savedRows.Select(x => new ResponseGoLiveOpeningItemJson { Id = x.Id, Value = x.Value, DueDate = x.DueDate, Status = x.Status }).ToList()
        });
    }
    public async Task<(Account Account, TransactionType Type, string Identity)> Validate(BranchUser actor, RequestGoLiveOpeningJson request, CancellationToken token)
    {
        var validation = new GoLiveOpeningFluentValidation().Validate(request);
        if (!validation.IsValid)
            throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());

        await readLocks.TransactionTypes(actor.BranchId, [request.TransactionTypeId], token);
        if (request.ClientId is { } clientToLock) await readLocks.Clients(actor.BranchId, [clientToLock], token);
        await readLocks.Operators(actor.BranchId, [request.RecordedByOperatorId], token);
        var account = await accounts.GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, actor.BranchId, token)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        if (account.Type != (request.IsCheque ? AccountType.Safe : AccountType.Tab))
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        if (request.ClientId is { } clientId)
            _ = await clients.GetActiveByIdAndBranchIdAsNoTracking(clientId, actor.BranchId)
                ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        _ = await operators.GetActiveByIdAndBranchIdAsNoTracking(request.RecordedByOperatorId, actor.BranchId, token)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(request.TransactionTypeId, actor.BranchId, token)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        if (!type.Category.Active || type.FinancialClass != FinancialClass.CaixaAndClientMoney || type.MonthlyFinancialRole != MonthlyFinancialRole.None || type.FiadoPaymentKind is not null || type.SystemRole is not null || type.Category.DefaultDirection != Direction.Out
            || request.IsCheque && (type.SettlementInstrument != SettlementInstrument.Cheque || type.RequiresTabAccountAndClient)
            || !request.IsCheque && (!type.RequiresTabAccountAndClient || type.SettlementInstrument != SettlementInstrument.None))
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        if (request.AsOfDate > clock.LocalBusinessDate(clock.UtcNow()))
            throw new OnValidationException([ResourcesErrorMessages.GOLIVE_OPENING_INVALID]);
        await locks.EnsureNotLocked(actor.BranchId, request.AsOfDate, ResourcesErrorMessages.TRANSACTION_DATE_LOCKED, token);
        var identity = request.IsCheque ? $"cheque:{request.AccountId}:{request.SourceReference.Trim()}"
            : $"fiado:{request.AccountId}:{request.ClientId}";
        if (!request.IsCheque && request.ClientId is { } importedClient && await openings.HasImportedDebt(actor.BranchId, request.AccountId, importedClient, token))
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_EXISTS);
        if (await openings.Exists(actor.BranchId, identity, token))
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_EXISTS);
        return (account, type, identity);
    }

}
