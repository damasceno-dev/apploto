using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques.Custody;

/// <summary>
/// <c>POST /cheque/custody</c> (M9.7 Phase 2.2, §6.19 O10): the listed cheques move into a cofre. Custody only — each
/// receivable stays open with its amount; no cashbox, cofre ledger, bank or session row is written. Each cheque gets a
/// <c>CustodyMoved</c> event naming where the paper came from and where it went; all or none.
/// </summary>
public class ChequeCustodyUseCase(
    ChequeCommandRunner runner,
    ChequeReceivableGuard chequeGuard,
    SettlementEventDating dating,
    ReceivableResponseBuilder responseBuilder,
    IAccountsRepository accountsRepository,
    ISettlementEventsRepository settlementEventsRepository,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /cheque/custody";

    public Task<ResponseChequeOperationJson> Execute(
        RequestChequeCustodyJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ChequeCustodyFluentValidation(), request);
        var transactionIds = request.Items.Select(item => item.TransactionId).ToList();

        return runner.Run(
            IdempotencyEndpoint,
            request,
            idempotencyKey,
            async (branchUser, token) =>
                [.. await chequeGuard.ResolveAccountIds(transactionIds, branchUser.BranchId, token), request.SafeAccountId],
            async (branchUser, token) =>
            {
                var safe = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.SafeAccountId, branchUser.BranchId, token);
                if (safe is null || safe.Type != AccountType.Safe)
                    throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_SAFE_NOT_FOUND);

                var dates = await dating.Resolve(branchUser.BranchId, request.OccurredOn, request.PostedOn, token);
                var cheques = await chequeGuard.Load(
                    request.Items.Select(item => (item.TransactionId, item.ExpectedVersion)).ToList(),
                    branchUser.BranchId,
                    dates.Today,
                    token);

                var appended = new List<SettlementEvent>();
                foreach (var cheque in cheques)
                {
                    if (ChequeLifecycleRules.CanMoveToSafe(cheque.Current.ChequeState) is false)
                        throw new ConflictException(ResourcesErrorMessages.CHEQUE_CUSTODY_STATE_INVALID);
                    if (cheque.Current.CustodyAccountId == safe.Id)
                        throw new ConflictException(ResourcesErrorMessages.CHEQUE_CUSTODY_SAME_LOCATION);
                    SettlementEventDating.EnsureChronology(cheque.Row, cheque.Last, dates);

                    appended.Add(ChequeReceivableGuard.NextEvent(
                        cheque,
                        SettlementEventKind.CustodyMoved,
                        cheque.Current.OutstandingValue,
                        dates,
                        branchUser.UserId,
                        new SettlementEventDetails
                        {
                            CustodyFromAccountId = cheque.Current.CustodyAccountId,
                            CustodyToAccountId = safe.Id
                        }));
                }

                foreach (var settlementEvent in appended)
                    await settlementEventsRepository.Add(settlementEvent, token);
                await unitOfWork.Commit(token);

                var receivables = await responseBuilder.Build(
                    cheques.Select(cheque => cheque.Row).ToList(),
                    branchUser.BranchId,
                    new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today),
                    token);
                return (appended[0].Id, new ResponseChequeOperationJson { Receivables = receivables });
            },
            ct);
    }
}
