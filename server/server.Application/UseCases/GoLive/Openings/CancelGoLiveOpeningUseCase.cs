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

public class CancelGoLiveOpeningUseCase(
    BankCommandRunner runner,
    IGoLiveOpeningsRepository openings,
    ISettlementEventsRepository events,
    LockDateGuard locks,
    IBranchClock clock,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseGoLiveOpeningJson> Execute(Guid id, RequestCancelGoLiveOpeningJson request, string? key, uint version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            throw new OnValidationException([ResourcesErrorMessages.GOLIVE_OPENING_INVALID]);
        return runner.Run<object, ResponseGoLiveOpeningJson>("POST /go-live/openings/{id}/cancel", new { Id = id, Request = request, Version = version }, key,
            async (actor, token) =>
            {
                var opening = await openings.Get(actor.BranchId, id, token)
                    ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
                return [actor.BranchId, opening.AccountId, opening.ClientId ?? opening.AccountId];
            },
            (actor, token) => Stage(actor, id, request, version, token), ct);
    }

    public async Task<(Guid ResourceId, ResponseGoLiveOpeningJson Response)> Stage(BranchUser actor, Guid id, RequestCancelGoLiveOpeningJson request, uint version, CancellationToken token)
    {

        var opening = await openings.GetForUpdate(actor.BranchId, id, token)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
        if (opening.Version != version) throw new ConflictException(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);
        if (!opening.Active)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_ALREADY_CANCELLED);
        if (await openings.GetSourceImportId(actor.BranchId, id, token) is not null)
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_IMPORT_OWNED);
        await locks.EnsureNotLocked(actor.BranchId, opening.AsOfDate, ResourcesErrorMessages.TRANSACTION_DATE_LOCKED, token);
        // Conservative before-consumption boundary: any recorded client credit blocks cancellation.
        // FIFO is branch/client wide and cannot be reversed by rewriting the opening after repayment.
        if (!opening.IsCheque && opening.ClientCreditObservedAt is not null) throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_CONSUMED);
        if (!opening.IsCheque && opening.ClientId is { } clientId && await openings.HasClientCredit(actor.BranchId, clientId, token))
            throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_CONSUMED);
        var rows = await openings.Rows(actor.BranchId, id, token);
        foreach (var row in rows)
        {
            if (await events.ExistsByReceivableIdAsNoTracking(row.Id, token))
                throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_CONSUMED);
        }
        var now = clock.UtcNow();
        foreach (var row in rows)
        {
            row.Status = TransactionStatus.Cancelled;
            row.CancelledAt = now;
            row.CancelledByUserId = actor.UserId;
            row.CancellationReason = request.Reason.Trim();
        }
        opening.Active = false;
        opening.CancelledAt = now;
        opening.CancelledByUserId = actor.UserId;
        opening.CancellationReason = request.Reason.Trim();
        if (!await unitOfWork.TryCommitOptimistic(token)) throw new ConflictException(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);
        return (id, new ResponseGoLiveOpeningJson
        {
            Id = id,
            Version = opening.Version,
            AccountId = opening.AccountId,
            ClientId = opening.ClientId,
            IsCheque = opening.IsCheque,
            AsOfDate = opening.AsOfDate,
            SourceReference = opening.SourceReference,
            SourceEvidence = opening.SourceEvidence,
            PreviousId = opening.PreviousId,
            Active = false,
            TransactionIds = rows.Select(x => x.Id).ToList(),
            RecordedByOperatorId = rows.FirstOrDefault()?.RecordedByOperatorId ?? Guid.Empty,
            ClientCreditObserved = opening.ClientCreditObservedAt is not null,
            Items = rows.Select(x => new ResponseGoLiveOpeningItemJson { Id = x.Id, Value = x.Value, DueDate = x.DueDate, Status = x.Status }).ToList()
        });
    }
}
