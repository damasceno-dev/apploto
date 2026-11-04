using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques.Return;

/// <summary>
/// <c>POST /cheque/{transactionId}/return</c> — <i>Cheque devolvido</i> (M9.7 Phase 2.3). Appends a
/// <c>ChequeReturned</c> event that reverses the exact current deposit event: the claim reopens for the deposited
/// amount, and the deposit's pending bank receipt now expects that amount less (its allocation is marked returned by
/// this event, so reconciliation can never confirm it). The client must be identified — the cheque's own client when it
/// has one — with the next expected deposit date and an optional <i>alínea</i>. A return after the deposit month locked
/// is posted in an open period; the original sale, deposit event and receipt rows are never edited.
/// </summary>
public class ChequeReturnUseCase(
    ChequeCommandRunner runner,
    ChequeReceivableGuard chequeGuard,
    SettlementEventDating dating,
    ReceivableResponseBuilder responseBuilder,
    ITransactionsRepository transactionsRepository,
    ISettlementEventsRepository settlementEventsRepository,
    IBankReceiptsRepository bankReceiptsRepository,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /cheque/{transactionId}/return";

    public Task<ResponseChequeOperationJson> Execute(
        Guid transactionId,
        RequestChequeReturnJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ChequeReturnFluentValidation(), request);

        return runner.Run(
            IdempotencyEndpoint,
            new ChequeReturnPayload(transactionId, request),
            idempotencyKey,
            async (branchUser, token) =>
            {
                var accountIds = (await chequeGuard.ResolveAccountIds([transactionId], branchUser.BranchId, token)).ToList();
                // Events are immutable, so the deposit's receipt account can be read before the scope and locked with it.
                var events = await settlementEventsRepository.ListByReceivableIdsAndBranchIdAsNoTracking(
                    [transactionId], branchUser.BranchId, token);
                var deposit = events.SingleOrDefault(e => e.Id == request.DepositEventId);
                if (deposit?.BankReceiptId is { } receiptId &&
                    await bankReceiptsRepository.GetByIdAndBranchIdAsNoTracking(receiptId, branchUser.BranchId, token) is { } receipt)
                    accountIds.Add(receipt.BankAccountId);
                return accountIds;
            },
            async (branchUser, token) =>
            {
                var dates = await dating.Resolve(branchUser.BranchId, request.OccurredOn, request.PostedOn, token);
                var cheque = (await chequeGuard.Load(
                    [(transactionId, request.ExpectedVersion)], branchUser.BranchId, dates.Today, token))[0];

                if (ChequeLifecycleRules.CanReturn(cheque.Current.ChequeState) is false)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_RETURN_STATE_INVALID);
                if (cheque.Current.CurrentDepositEventId != request.DepositEventId)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_RETURN_DEPOSIT_MISMATCH);

                _ = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.ClientId, branchUser.BranchId)
                    ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
                if (cheque.IdentifiedClientId is { } identified && identified != request.ClientId)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_CLIENT_MISMATCH);
                SettlementEventDating.EnsureChronology(cheque.Row, cheque.Last, dates);

                var deposit = cheque.Events.Single(e => e.Id == request.DepositEventId);
                var returned = ChequeReceivableGuard.NextEvent(
                    cheque,
                    SettlementEventKind.ChequeReturned,
                    deposit.Amount,
                    dates,
                    branchUser.UserId,
                    new SettlementEventDetails
                    {
                        ReversesEventId = deposit.Id,
                        ClientId = request.ClientId,
                        NextExpectedDepositDate = request.NextExpectedDepositDate.Date,
                        ReturnReason = string.IsNullOrWhiteSpace(request.ReturnReason) ? null : request.ReturnReason.Trim()
                    });
                await settlementEventsRepository.Add(returned, token);
                await unitOfWork.Commit(token);

                var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchUser.BranchId, token);
                var receivables = await responseBuilder.Build(
                    [row!], branchUser.BranchId, new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today), token);
                var receipt = await responseBuilder.BuildReceipt(deposit.BankReceiptId!.Value, branchUser.BranchId, token);
                return (returned.Id, new ResponseChequeOperationJson { Receivables = receivables, BankReceipt = receipt });
            },
            ct);
    }
}

/// <summary>The fingerprinted payload: the route id is part of the request identity.</summary>
internal sealed record ChequeReturnPayload(Guid TransactionId, RequestChequeReturnJson Request);
