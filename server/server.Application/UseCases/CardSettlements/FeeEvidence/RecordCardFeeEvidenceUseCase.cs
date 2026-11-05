using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.CardSettlements.FeeEvidence;

public class RecordCardFeeEvidenceUseCase(
    BankCommandRunner runner,
    ICardSettlementsRepository cards,
    IBankReceiptsRepository receipts,
    IAccountsRepository accounts,
    SettlementEventDating dating,
    ReceivableResponseBuilder responses,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseBankReceiptJson> Execute(RequestCardFeeEvidenceJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new CardFeeEvidenceFluentValidation(), request);
        return runner.Run<RequestCardFeeEvidenceJson, ResponseBankReceiptJson>("POST /card-settlement/fee-evidence", request, key,
            async (actor, token) =>
            {
                var batch = await Batch(request.BatchId, actor.BranchId, token);
                var receipt = await receipts.GetByIdAndBranchIdAsNoTracking(batch.BankReceiptId, actor.BranchId, token) ?? throw Conflict();
                return [receipt.BankAccountId];
            },
            async (actor, token) =>
            {
                var batch = await Batch(request.BatchId, actor.BranchId, token);
                var receipt = await receipts.GetByIdAndBranchIdAsNoTracking(batch.BankReceiptId, actor.BranchId, token) ?? throw Conflict();
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(receipt.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                var history = await receipts.ListFeeEvidenceAsNoTracking(batch.Id, actor.BranchId, token);
                var latest = history.LastOrDefault();
                if (receipt.Status != BankReceiptStatus.Pending || receipt.Version != request.ReceiptVersion ||
                    request.ExpectedFeeEvidenceVersion != (latest?.Sequence ?? 0) || request.ConfirmedFee >= batch.GrossAmount) throw Conflict();
                var dates = await dating.Resolve(actor.BranchId, request.OccurredOn, request.PostedOn, token);
                if (dates.OccurredOn < (latest?.OccurredOn ?? receipt.OccurredOn) || dates.PostedOn < (latest?.PostedOn ?? receipt.PostedOn)) throw Conflict();
                var evidence = new CardSettlementFeeEvidence
                {
                    BranchId = actor.BranchId, BatchId = batch.Id, Sequence = (latest?.Sequence ?? 0) + 1,
                    ConfirmedFee = request.ConfirmedFee,
                    ObservedNetAmount = request.ObservedNetAmount ?? latest?.ObservedNetAmount ?? batch.ObservedNetAmount,
                    Basis = request.Basis.Trim(), Evidence = request.Evidence.Trim(), Reason = request.Reason.Trim(),
                    OccurredOn = dates.OccurredOn, PostedOn = dates.PostedOn, CreatedByUserId = actor.UserId
                };
                await cards.AddFeeEvidence(evidence, token);
                await unitOfWork.Commit(token);
                return (evidence.Id, (await responses.BuildReceipt(receipt.Id, actor.BranchId, token))!);
            }, ct);
    }

    private async Task<CardSettlementBatch> Batch(Guid id, Guid branchId, CancellationToken ct) =>
        await cards.GetBatchAsNoTracking(id, branchId, ct) ?? throw Conflict();
    private static ConflictException Conflict() => new(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
}
