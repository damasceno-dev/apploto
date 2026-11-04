using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques.Deposit;

/// <summary>
/// <c>POST /cheque/deposit</c> — <i>Depositar</i> (M9.7 Phase 2.2, §6.19 O11). One batch into one bank account: every
/// listed cheque gets its <c>ChequeDeposited</c> event (paid = deposited, product Q2) for its whole face value, and the
/// batch gets ONE pending <see cref="BankReceipt"/> for their sum; the deposit events are the receipt's allocations. The
/// receipt is not a ledger row and does not claim the money arrived — statement reconciliation (Phase 5) confirms it.
/// Every cheque, version, date, account and duplicate is checked before anything is written: one failure deposits
/// nothing. No cashbox, cofre or session row is written; the original sale, even in a locked month, is untouched.
/// </summary>
public class ChequeDepositUseCase(
    ChequeCommandRunner runner,
    ChequeReceivableGuard chequeGuard,
    SettlementEventDating dating,
    ReceivableResponseBuilder responseBuilder,
    IAccountsRepository accountsRepository,
    ISettlementEventsRepository settlementEventsRepository,
    IBankReceiptsRepository bankReceiptsRepository,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /cheque/deposit";

    public Task<ResponseChequeOperationJson> Execute(
        RequestChequeDepositJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ChequeDepositFluentValidation(), request);
        var transactionIds = request.Items.Select(item => item.TransactionId).ToList();

        return runner.Run(
            IdempotencyEndpoint,
            request,
            idempotencyKey,
            async (branchUser, token) =>
                [.. await chequeGuard.ResolveAccountIds(transactionIds, branchUser.BranchId, token), request.BankAccountId],
            async (branchUser, token) =>
            {
                var bank = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.BankAccountId, branchUser.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount)
                    throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);

                var dates = await dating.Resolve(branchUser.BranchId, request.OccurredOn, request.PostedOn, token);
                var cheques = await chequeGuard.Load(
                    request.Items.Select(item => (item.TransactionId, item.ExpectedVersion)).ToList(),
                    branchUser.BranchId,
                    dates.Today,
                    token);

                foreach (var cheque in cheques)
                {
                    if (ChequeLifecycleRules.CanDeposit(cheque.Current.ChequeState) is false)
                        throw new ConflictException(ResourcesErrorMessages.CHEQUE_DEPOSIT_STATE_INVALID);
                    ChequeReceivableGuard.EnsureWhole(cheque);
                    SettlementEventDating.EnsureChronology(cheque.Row, cheque.Last, dates);
                }

                var receipt = new BankReceipt
                {
                    BranchId = branchUser.BranchId,
                    BankAccountId = bank.Id,
                    SourceKind = BankReceiptSourceKind.ChequeDeposit,
                    Status = BankReceiptStatus.Pending,
                    OccurredOn = dates.OccurredOn,
                    PostedOn = dates.PostedOn,
                    Amount = cheques.Sum(cheque => cheque.Current.OutstandingValue),
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    CreatedByUserId = branchUser.UserId
                };
                await bankReceiptsRepository.Add(receipt, token);

                foreach (var cheque in cheques)
                {
                    await settlementEventsRepository.Add(
                        ChequeReceivableGuard.NextEvent(
                            cheque,
                            SettlementEventKind.ChequeDeposited,
                            cheque.Current.OutstandingValue,
                            dates,
                            branchUser.UserId,
                            new SettlementEventDetails { BankReceiptId = receipt.Id }),
                        token);
                }
                await unitOfWork.Commit(token);

                var receivables = await responseBuilder.Build(
                    cheques.Select(cheque => cheque.Row).ToList(),
                    branchUser.BranchId,
                    new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today),
                    token);
                var receiptResponse = await responseBuilder.BuildReceipt(receipt.Id, branchUser.BranchId, token);
                return (receipt.Id, new ResponseChequeOperationJson { Receivables = receivables, BankReceipt = receiptResponse });
            },
            ct);
    }
}
