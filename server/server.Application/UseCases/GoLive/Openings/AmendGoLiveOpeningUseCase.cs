using server.Application.Services.Receivables;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Openings;

public class AmendGoLiveOpeningUseCase(BankCommandRunner runner, IGoLiveOpeningsRepository openings, CancelGoLiveOpeningUseCase cancel, RecordGoLiveOpeningUseCase record, IUnitOfWork unitOfWork)
{
    public Task<ResponseGoLiveOpeningJson> Execute(Guid id, RequestAmendGoLiveOpeningJson request, uint version, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || request.Opening is null) throw new OnValidationException([ResourcesErrorMessages.GOLIVE_OPENING_INVALID]);
        var validation = new GoLiveOpeningFluentValidation().Validate(request.Opening);
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());
        return runner.Run<object, ResponseGoLiveOpeningJson>("POST /go-live/openings/{id}/amend", new { Id = id, Request = request, Version = version }, key,
            async (actor, token) =>
            {
                var prior = await openings.Get(actor.BranchId, id, token) ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
                return [actor.BranchId, prior.AccountId, request.Opening.AccountId, prior.ClientId ?? prior.AccountId, request.Opening.ClientId ?? request.Opening.AccountId];
            },
            async (actor, token) =>
            {
                var prior = await openings.GetForUpdate(actor.BranchId, id, token) ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
                if (prior.AccountId != request.Opening.AccountId || prior.IsCheque != request.Opening.IsCheque
                    || !prior.IsCheque && prior.ClientId != request.Opening.ClientId || prior.SourceReference != request.Opening.SourceReference.Trim())
                    throw new ConflictException(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
                await cancel.Stage(actor, id, new RequestCancelGoLiveOpeningJson { Reason = request.Reason }, version, token);
                await unitOfWork.Commit(token);
                return await record.Stage(actor, request.Opening, token, prior.Id);
            }, ct);
    }
}
