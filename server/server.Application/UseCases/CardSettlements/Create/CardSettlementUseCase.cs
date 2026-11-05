using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.CardSettlements.Create;

public class CardSettlementUseCase(
    BankCommandRunner runner,
    ICardSettlementsRepository cards,
    IBankingReadLocks readLocks,
    ITransactionsRepository transactions,
    IAccountsRepository accounts,
    ISettlementEventsRepository events,
    IBankReceiptsRepository receipts,
    IHolidaysRepository holidays,
    SettlementEventDating dating,
    ReceivableResponseBuilder responses,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseCardSettlementJson> Execute(RequestCardSettlementJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new CardSettlementFluentValidation(), request);
        return runner.Run<RequestCardSettlementJson, ResponseCardSettlementJson>("POST /card-settlement", request, key,
            async (actor, token) =>
            {
                var provider = await Provider(request.ProviderId, actor.BranchId, token);
                var ids = new List<Guid> { provider.BankAccountId };
                foreach (var item in request.Items) ids.Add((await Row(item.TransactionId, actor.BranchId, token)).AccountId);
                return ids;
            },
            async (actor, token) =>
            {
                var provider = await Provider(request.ProviderId, actor.BranchId, token);
                if (provider.Version != request.ProviderVersion || await cards.BatchIdentityExists(provider.Id, request.ExternalIdentity.Trim(), token)) throw Conflict();
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(provider.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                var dates = await dating.Resolve(actor.BranchId, request.OccurredOn, request.PostedOn, token);
                if (request.PlanningBasisOn.Date > dates.OccurredOn) throw Conflict();
                await readLocks.Transactions(actor.BranchId, request.Items.Select(i => i.TransactionId).ToList(), token);
                var chain = await events.ListByReceivableIdsAndBranchIdAsNoTracking(request.Items.Select(i => i.TransactionId).ToList(), actor.BranchId, token);
                var rows = new List<Transaction>();
                var gross = request.Items.Sum(i => i.GrossAmount);
                if (!CardSettlementFluentValidation.ValidAmount(gross) || request.ConfirmedFee >= gross) throw Conflict();
                // Unknown fee: the gross expectation is held for review and cannot be reconciled until supported.
                var net = gross - (request.ConfirmedFee ?? 0m);
                var receipt = new BankReceipt
                {
                    BranchId = actor.BranchId, BankAccountId = bank.Id, SourceKind = BankReceiptSourceKind.ProviderSettlement,
                    OccurredOn = dates.OccurredOn, PostedOn = dates.PostedOn, Amount = net, CreatedByUserId = actor.UserId
                };
                await receipts.Add(receipt, token);
                foreach (var item in request.Items)
                {
                    var row = await Row(item.TransactionId, actor.BranchId, token);
                    var own = chain.Where(e => e.ReceivableTransactionId == row.Id).OrderBy(e => e.Sequence).ToList();
                    var instrument = own.FirstOrDefault(e => e.Provenance == SettlementEventProvenance.Command)?.SettlementInstrument ?? row.PostedSettlementInstrument;
                    if (!row.Active || row.Status != TransactionStatus.Active || row.GenerationKind is not null || instrument != provider.Instrument) throw new ConflictException(ResourcesErrorMessages.CARD_CLAIM_INVALID);
                    var current = ReceivableResponseBuilder.Project(row, own, new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today));
                    if (row.Version != item.TransactionVersion || current.Version != item.ExpectedVersion || item.GrossAmount > current.OutstandingValue) throw Conflict();
                    SettlementEventDating.EnsureChronology(row, own.LastOrDefault(), dates);
                    await events.Add(new SettlementEvent
                    {
                        BranchId = actor.BranchId, ReceivableTransactionId = row.Id, Sequence = current.Version + 1,
                        PreviousEventId = own.LastOrDefault()?.Id, Kind = SettlementEventKind.ProviderSettled,
                        Amount = item.GrossAmount, OutstandingDelta = -item.GrossAmount, OccurredOn = dates.OccurredOn,
                        PostedOn = dates.PostedOn, Provenance = SettlementEventProvenance.Command,
                        SettlementInstrument = instrument.Value, BankReceiptId = receipt.Id, CreatedByUserId = actor.UserId
                    }, token);
                    rows.Add(row);
                }
                var holidayDates = (await holidays.ListActiveDatesByBranchIdAsNoTracking(actor.BranchId, token)).ToHashSet();
                var planned = PlanArrival(request.PlanningBasisOn.Date, provider.DelayDays, provider.BusinessDays, holidayDates);
                var batch = new CardSettlementBatch
                {
                    BranchId = actor.BranchId, ProviderId = provider.Id, ExternalIdentity = request.ExternalIdentity.Trim(),
                    BankReceiptId = receipt.Id, ProviderName = provider.Name, Brand = provider.Brand, PlannedArrivalOn = planned,
                    GrossAmount = gross, ConfirmedFee = request.ConfirmedFee, ManagerConfirmedFee = request.ManagerConfirmedFee,
                    FeeBasis = string.IsNullOrWhiteSpace(request.FeeBasis) ? null : request.FeeBasis.Trim(),
                    FeeEvidence = string.IsNullOrWhiteSpace(request.FeeEvidence) ? null : request.FeeEvidence.Trim(), ObservedNetAmount = request.ObservedNetAmount,
                    CreatedByUserId = actor.UserId
                };
                await cards.AddBatch(batch, token);
                await unitOfWork.Commit(token);
                return (batch.Id, new ResponseCardSettlementJson
                {
                    Id = batch.Id, ProviderId = provider.Id, ExternalIdentity = batch.ExternalIdentity, PlannedArrivalOn = planned,
                    GrossAmount = gross, ConfirmedFee = batch.ConfirmedFee, ExpectedNetAmount = batch.ConfirmedFee is null ? null : net,
                    FeeUnknown = batch.ConfirmedFee is null, ObservedNetAmount = batch.ObservedNetAmount,
                    UnmatchedDifference = batch.ObservedNetAmount is not null && batch.ConfirmedFee is not null ? batch.ObservedNetAmount - net : null,
                    BankReceipt = (await responses.BuildReceipt(receipt.Id, actor.BranchId, token))!,
                    Receivables = await responses.Build(rows, actor.BranchId, new ReceivableCutoff(ReceivableCutoffBasis.Posted, dates.Today), token)
                });
            }, ct);
    }

    internal static DateTime PlanArrival(DateTime basis, int days, bool businessDays, IReadOnlySet<DateOnly> holidays)
    {
        var result = basis;
        if (days == 0 && businessDays)
            while (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays.Contains(DateOnly.FromDateTime(result)))
                result = result.AddDays(1);
        for (var count = 0; count < days;)
        {
            result = result.AddDays(1);
            if (!businessDays || result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(DateOnly.FromDateTime(result))) count++;
        }
        return result;
    }

    private async Task<CardProvider> Provider(Guid id, Guid branch, CancellationToken ct) => await cards.GetProviderAsNoTracking(id, branch, ct) ?? throw new NotFoundException(ResourcesErrorMessages.CARD_PROVIDER_NOT_FOUND);
    private async Task<Transaction> Row(Guid id, Guid branch, CancellationToken ct) => await transactions.GetByIdAndBranchIdAsNoTracking(id, branch, ct) ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    private static ConflictException Conflict() => new(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
}
