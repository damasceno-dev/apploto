using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques.Represent;

/// <summary>
/// <c>POST /cheque/{transactionId}/represent</c> — <i>Reapresentar</i> (M9.7 Phase 2.3). A returned cheque becomes
/// depositable again: a <c>ChequeRepresented</c> event with no amount effect; the next <i>Depositar</i> is a new
/// attempt with its own receipt. Former deposits and returns stay in the chain. The version check and the
/// <c>Idempotency-Key</c> make a retried request return the same attempt instead of appending another.
/// </summary>
public class ChequeRepresentUseCase(
    ChequeCommandRunner runner,
    ChequeReceivableGuard chequeGuard,
    SettlementEventDating dating,
    ReceivableResponseBuilder responseBuilder,
    ITransactionsRepository transactionsRepository,
    ISettlementEventsRepository settlementEventsRepository,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /cheque/{transactionId}/represent";

    public Task<ResponseChequeOperationJson> Execute(
        Guid transactionId,
        RequestChequeRepresentJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ChequeRepresentFluentValidation(), request);

        return runner.Run(
            IdempotencyEndpoint,
            new ChequeRepresentPayload(transactionId, request),
            idempotencyKey,
            async (branchUser, token) => await chequeGuard.ResolveAccountIds([transactionId], branchUser.BranchId, token),
            async (branchUser, token) =>
            {
                var dates = await dating.Resolve(branchUser.BranchId, request.OccurredOn, request.PostedOn, token);
                var cheque = (await chequeGuard.Load(
                    [(transactionId, request.ExpectedVersion)], branchUser.BranchId, dates.Today, token))[0];

                if (ChequeLifecycleRules.CanRepresent(cheque.Current.ChequeState) is false)
                    throw new ConflictException(ResourcesErrorMessages.CHEQUE_REPRESENT_STATE_INVALID);
                ChequeReceivableGuard.EnsureWhole(cheque);
                SettlementEventDating.EnsureChronology(cheque.Row, cheque.Last, dates);

                var represented = ChequeReceivableGuard.NextEvent(
                    cheque, SettlementEventKind.ChequeRepresented, cheque.Current.OutstandingValue, dates, branchUser.UserId);
                await settlementEventsRepository.Add(represented, token);
                await unitOfWork.Commit(token);

                var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchUser.BranchId, token);
                var receivables = await responseBuilder.Build(
                    [row!], branchUser.BranchId, new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today), token);
                return (represented.Id, new ResponseChequeOperationJson { Receivables = receivables });
            },
            ct);
    }
}

internal sealed record ChequeRepresentPayload(Guid TransactionId, RequestChequeRepresentJson Request);
