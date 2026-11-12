using server.Application.Services.Transactions;
using server.Application.Services.Reports;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

public class RecordHourBankEntryUseCase(PeopleCommandRunner runner, IEmployeeRecordsRepository records, IUnitOfWork unitOfWork, IBranchClock clock)
{
    public Task<ResponseHourBankEntryJson> Execute(RequestHourBankEntryJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new HourBankEntryFluentValidation(), request);
        return runner.Run<RequestHourBankEntryJson, ResponseHourBankEntryJson>(PeopleCommandRunner.Endpoint("hour-bank-entry", request.PreviousId), request, key, version, request.OperatorId,
            async (actor, token) =>
            {
                var rows = await records.List<HourBankEntry>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
            },
            (actor, locked, token) => Stage(actor, locked, request, version, token), ct);
    }

    public async Task<(Guid Id, ResponseHourBankEntryJson Response)> Stage(BranchUser actor, DateTime locked, RequestHourBankEntryJson request, uint? version, CancellationToken token)
    {
        PeopleValidation.Validate(new HourBankEntryFluentValidation(), request);

                var rows = await records.List<HourBankEntry>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                PeopleHistory.Require(request.SourceDate <= request.PostedOn);
                if (prior != null) PeopleHistory.Require(prior.OperatorId == request.OperatorId && prior.Kind == request.Kind && prior.SourceDate == request.SourceDate && prior.ExternalIdentity == request.ExternalIdentity.Trim() && !prior.Cancelled && request.PostedOn >= prior.PostedOn);
                else
                {
                    PeopleHistory.Require(!request.Cancelled);
                    if (request.Kind == HourBankEntryKind.Opening)
                    {
                        PeopleHistory.Open(request.SourceDate, locked);
                        if (rows.Any(r => r.OperatorId == request.OperatorId && r.Kind == HourBankEntryKind.Opening)) throw new ConflictException(ResourcesErrorMessages.PEOPLE_DUPLICATE_SOURCE);
                        PeopleHistory.Require(!string.IsNullOrWhiteSpace(request.ExternalIdentity));
                    }
                    else if (!rows.Any(r => r.OperatorId == request.OperatorId && r.Kind == HourBankEntryKind.Opening && r.SourceDate <= request.SourceDate && r.PostedOn <= request.PostedOn))
                        throw new OnValidationException([ResourcesErrorMessages.PEOPLE_OPENING_REQUIRED]);
                    if (!string.IsNullOrWhiteSpace(request.ExternalIdentity) && rows.Any(r => r.ExternalIdentity == request.ExternalIdentity.Trim())) throw new ConflictException(ResourcesErrorMessages.PEOPLE_DUPLICATE_SOURCE);
                }
                // Opening is an evidenced anchor; corrections preserve its date and append the difference in an open period.
                PeopleHistory.Require(request.Kind != HourBankEntryKind.Opening || !request.Cancelled);
                if (!PeopleValidation.Precision((request.Cancelled ? 0 : request.SignedHours) - (prior == null || prior.Cancelled ? 0 : prior.SignedHours), 4))
                    throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
                var row = new HourBankEntry
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    OperatorId = request.OperatorId, Kind = request.Kind, SourceDate = request.SourceDate, SignedHours = request.SignedHours,
                    SignedContribution = (request.Cancelled ? 0 : request.SignedHours) - (prior == null || prior.Cancelled ? 0 : prior.SignedHours),
                    Cancelled = request.Cancelled, ExternalIdentity = request.ExternalIdentity.Trim(),
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
    }
}
