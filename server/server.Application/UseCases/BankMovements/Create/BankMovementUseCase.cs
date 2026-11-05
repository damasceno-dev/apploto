using FluentValidation;
using server.Application.Services.Receivables;
using server.Application.UseCases.CardSettlements.Create;
using server.Application.UseCases.CardProviders.Create;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BankMovements.Create;

/// <summary>
/// Posts an internal-transfer pair, or an explicitly observed reimbursement/fee. The bank ledger entry is posted once
/// here; its receipt stays pending external verification. Matching confirms that existing entry and adds no credit.
/// Cash recolhimento has no cashbox/session/variance leg. Cheques use the existing custody/deposit commands instead.
/// </summary>
public class BankMovementUseCase(
    BankCommandRunner runner,
    IAccountsRepository accounts,
    IBankingReadLocks readLocks,
    ITransactionTypesRepository types,
    ITransactionsRepository transactions,
    IOperatorsRepository operators,
    IBankMovementsRepository movements,
    IBankReceiptsRepository receipts,
    SettlementEventDating dating,
    ReceivableResponseBuilder responses,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseBankMovementJson> Execute(RequestBankMovementJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new BankMovementFluentValidation(), request);
        return runner.Run<RequestBankMovementJson, ResponseBankMovementJson>("POST /bank-movement", request, key,
            (_, _) => Task.FromResult<IReadOnlyCollection<Guid>>(request.SafeAccountId is { } safe ? [request.BankAccountId, safe] : [request.BankAccountId]),
            async (actor, token) =>
            {
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(request.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                if (await operators.GetActiveByIdAndBranchIdAsNoTracking(request.RecordedByOperatorId, actor.BranchId, token) is null) throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
                var dates = await dating.Resolve(actor.BranchId, request.OccurredOn, request.PostedOn, token);
                var classification = request.Kind switch
                {
                    BankMovementKind.SafeCollection => FinancialClass.InternalTransfer,
                    BankMovementKind.CaixaPrizeReimbursement => FinancialClass.CaixaAndClientMoney,
                    BankMovementKind.BankFee => FinancialClass.OperatingExpenseAndLoss,
                    _ => throw new OnValidationException([ResourcesErrorMessages.BANKING_INPUT_INVALID])
                };
                var direction = request.Kind == BankMovementKind.BankFee ? Direction.Out : Direction.In;
                await readLocks.TransactionTypes(actor.BranchId, request.SourceTransactionTypeId is { } sourceTypeId ? [request.BankTransactionTypeId, sourceTypeId] : [request.BankTransactionTypeId], token);
                var bankType = await Type(request.BankTransactionTypeId, actor.BranchId, direction, classification);
                var bankRow = MakeRow(bank.Id, bankType, direction, request, dates, actor);
                Transaction? source = null;
                if (request.SafeAccountId is { } safeId)
                {
                    var safe = await accounts.GetActiveByIdAndBranchIdAsNoTracking(safeId, actor.BranchId, token);
                    if (safe is null || safe.Type != AccountType.Safe) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_SAFE_NOT_FOUND);
                    var sourceType = await Type(request.SourceTransactionTypeId!.Value, actor.BranchId, Direction.Out, classification);
                    source = MakeRow(safe.Id, sourceType, Direction.Out, request, dates, actor);
                    await transactions.Add(source, token);
                }
                await transactions.Add(bankRow, token);
                var receipt = new BankReceipt
                {
                    BranchId = actor.BranchId, BankAccountId = bank.Id,
                    SourceKind = request.Kind switch { BankMovementKind.SafeCollection => BankReceiptSourceKind.SafeCollection, BankMovementKind.CaixaPrizeReimbursement => BankReceiptSourceKind.CaixaPrizeReimbursement, _ => BankReceiptSourceKind.BankFee },
                    OccurredOn = dates.OccurredOn, PostedOn = dates.PostedOn, Amount = request.Amount,
                    Description = request.Evidence.Trim(), CreatedByUserId = actor.UserId
                };
                await receipts.Add(receipt, token);
                var movement = new BankMovement
                {
                    BranchId = actor.BranchId, Kind = request.Kind, BankReceiptId = receipt.Id, BankTransactionId = bankRow.Id,
                    SourceTransactionId = source?.Id, Evidence = request.Evidence.Trim(), CreatedByUserId = actor.UserId
                };
                await movements.Add(movement, token);
                await unitOfWork.Commit(token);
                return (movement.Id, new ResponseBankMovementJson
                {
                    Id = movement.Id, Kind = movement.Kind, BankTransactionId = bankRow.Id, SourceTransactionId = source?.Id,
                    BankReceipt = (await responses.BuildReceipt(receipt.Id, actor.BranchId, token))!
                });
            }, ct);
    }

    private async Task<TransactionType> Type(Guid id, Guid branchId, Direction direction, FinancialClass classification)
    {
        var type = await types.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(id, branchId) ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        if (type.SystemRole is not null ||
            !type.Category.Active || type.Category.DefaultDirection != direction || type.FinancialClass != classification ||
            type.SettlementInstrument != SettlementInstrument.None || type.MonthlyFinancialRole != MonthlyFinancialRole.None || type.RequiresTabAccountAndClient)
            throw new ConflictException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
        return type;
    }
    private static Transaction MakeRow(
        Guid account,
        TransactionType type,
        Direction direction,
        RequestBankMovementJson request,
        SettlementEventDates dates,
        BranchUser actor) => new()
    {
        BranchId = actor.BranchId, AccountId = account, TransactionTypeId = type.Id, CategoryId = type.CategoryId,
        Direction = direction, Value = request.Amount, Date = dates.PostedOn, DueDate = dates.PostedOn,
        Description = request.Evidence.Trim(), RecordedByOperatorId = request.RecordedByOperatorId,
        CreatedByUserId = actor.UserId, GenerationKind = TransactionGenerationKind.BankMovement
    };
}
