using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques.ConvertToFiado;

/// <summary>
/// <c>POST /cheque/{transactionId}/convert-to-fiado</c> — <i>Converter em fiado</i> (M9.7 Phase 2.3, §6.19 O21). In one
/// commit the cheque's whole outstanding claim closes (<c>ChequeConvertedToFiado</c>) and the identified client's fiado
/// debt of the same amount opens on the chosen Tab: a protected Tab <c>Out</c> of the system type
/// <i>Cheque convertido em fiado</i> (<see cref="FiadoDebtOrigin.ChequeConversion"/>), dated on the posting date and
/// linked to the cheque. No cashbox <c>Out</c>/<c>In</c>, no counter-sale pairing, never both claims outstanding; later
/// repayments net it by the branch's FIFO like any fiado debt. A deposited cheque must be returned first.
/// </summary>
public class ChequeConvertToFiadoUseCase(
    ChequeCommandRunner runner,
    ChequeReceivableGuard chequeGuard,
    SettlementEventDating dating,
    ReceivableResponseBuilder responseBuilder,
    ITransactionsRepository transactionsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IAccountsRepository accountsRepository,
    IClientsRepository clientsRepository,
    ISettlementEventsRepository settlementEventsRepository,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /cheque/{transactionId}/convert-to-fiado";
    public const string DefaultDescription = "Cheque convertido em fiado";

    public Task<ResponseChequeOperationJson> Execute(
        Guid transactionId,
        RequestChequeConvertToFiadoJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ChequeConvertToFiadoFluentValidation(), request);

        return runner.Run(
            IdempotencyEndpoint,
            new ChequeConvertToFiadoPayload(transactionId, request),
            idempotencyKey,
            async (branchUser, token) =>
                [.. await chequeGuard.ResolveAccountIds([transactionId], branchUser.BranchId, token), request.TabAccountId],
            async (branchUser, token) =>
            {
                var tab = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.TabAccountId, branchUser.BranchId, token);
                if (tab is null || tab.Type != AccountType.Tab)
                    throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
                _ = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.ClientId, branchUser.BranchId)
                    ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);

                var dates = await dating.Resolve(branchUser.BranchId, request.OccurredOn, request.PostedOn, token);
                var cheque = (await chequeGuard.Load(
                    [(transactionId, request.ExpectedVersion)], branchUser.BranchId, dates.Today, token))[0];

                if (ChequeLifecycleRules.CanConvertToFiado(cheque.Current.ChequeState) is false)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_CONVERSION_STATE_INVALID);
                ChequeReceivableGuard.EnsureWhole(cheque);
                if (cheque.IdentifiedClientId is { } identified && identified != request.ClientId)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_CLIENT_MISMATCH);
                SettlementEventDating.EnsureChronology(cheque.Row, cheque.Last, dates);

                var debtType = await transactionTypesRepository.GetActiveSystemTypeByBranchIdAsNoTracking(
                    branchUser.BranchId, TransactionTypeSystemRole.ChequeFiadoConversion, token)
                    ?? throw new ConflictException(ResourcesErrorMessages.BRANCH_FINANCIAL_SEEDS_MISSING);

                var amount = cheque.Current.OutstandingValue;
                var debt = new Transaction
                {
                    Date = dates.PostedOn,
                    Value = amount,
                    Description = string.IsNullOrWhiteSpace(request.Description) ? DefaultDescription : request.Description.Trim(),
                    TransactionTypeId = debtType.Id,
                    CategoryId = debtType.CategoryId,
                    Direction = debtType.Category.DefaultDirection,
                    AccountId = tab.Id,
                    ClientId = request.ClientId,
                    DueDate = dates.PostedOn,
                    RecordedByOperatorId = cheque.Row.RecordedByOperatorId,
                    CreatedByUserId = branchUser.UserId,
                    Status = TransactionStatus.Active,
                    BranchId = branchUser.BranchId,
                    GenerationKind = TransactionGenerationKind.ChequeFiadoConversion,
                    GeneratedFromTransactionId = cheque.Row.Id
                };
                var converted = ChequeReceivableGuard.NextEvent(
                    cheque,
                    SettlementEventKind.ChequeConvertedToFiado,
                    amount,
                    dates,
                    branchUser.UserId,
                    new SettlementEventDetails { ClientId = request.ClientId, ConvertedTransactionId = debt.Id });

                await transactionsRepository.Add(debt, token);
                await settlementEventsRepository.Add(converted, token);
                await unitOfWork.Commit(token);

                var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchUser.BranchId, token);
                var receivables = await responseBuilder.Build(
                    [row!], branchUser.BranchId, new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today), token);
                return (converted.Id, new ResponseChequeOperationJson
                {
                    Receivables = receivables,
                    ConvertedTransactionId = debt.Id
                });
            },
            ct);
    }
}

internal sealed record ChequeConvertToFiadoPayload(Guid TransactionId, RequestChequeConvertToFiadoJson Request);
