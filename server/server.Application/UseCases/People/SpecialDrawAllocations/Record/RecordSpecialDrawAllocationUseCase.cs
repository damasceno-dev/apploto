using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

public class RecordSpecialDrawAllocationUseCase(
    PeopleCommandRunner runner,
    IEmployeeRecordsRepository records,
    IEmployeeStatementSources sources,
    IUnitOfWork unitOfWork,
    IBranchClock clock)
{
    public Task<ResponseSpecialDrawAllocationJson> Execute(RequestSpecialDrawAllocationJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new SpecialDrawAllocationFluentValidation(), request);
        return runner.Run<RequestSpecialDrawAllocationJson, ResponseSpecialDrawAllocationJson>(PeopleCommandRunner.Endpoint("special-draw-allocation", request.PreviousId), request, key, version, null,
            async (actor, token) =>
            {
                await sources.LockSpecialFee(actor.BranchId, request.SessionFieldEffectId, token);
                PeopleHistory.SourceExists(await records.List<SpecialDrawAllocation>(actor.BranchId, token), request.PreviousId);
                PeopleHistory.SourceExists(await records.List<SpecialDraw>(actor.BranchId, token), request.DrawId);
                if (await sources.SpecialFee(actor.BranchId, request.SessionFieldEffectId, token) is null) throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<SpecialDrawAllocation>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                var draws = await records.List<SpecialDraw>(actor.BranchId, token);
                var draw = (request.Cancelled ? draws : PeopleHistory.Heads(draws, r => r.PreviousId)).SingleOrDefault(r => r.Id == request.DrawId && (request.Cancelled || r.Enabled));
                var source = await sources.SpecialFee(actor.BranchId, request.SessionFieldEffectId, token);
                PeopleHistory.Require(source != null && source.SessionId == request.SessionId && source.SourceDate == request.SourceDate && request.SourceDate <= request.PostedOn && (request.Cancelled && prior != null || source.Official && source.Revision == request.SourceRevision));
                PeopleHistory.Require(draw != null && request.PostedOn >= draw.PostedOn && source!.SourceDate >= draw.PeriodFrom && source.SourceDate <= draw.PeriodTo);
                if (prior != null) PeopleHistory.Require(prior.DrawId == request.DrawId && prior.SessionFieldEffectId == request.SessionFieldEffectId && prior.SourceRevision == request.SourceRevision && prior.SourceDate == request.SourceDate && !prior.Cancelled && request.PostedOn >= prior.PostedOn);
                else { PeopleHistory.Open(request.SourceDate, locked); PeopleHistory.Require(!request.Cancelled); }
                var allocated = PeopleHistory.Heads(rows, r => r.PreviousId).Where(r => r.SessionFieldEffectId == request.SessionFieldEffectId && r.Id != prior?.Id && !r.Cancelled).Sum(r => r.Amount);
                if (!request.Cancelled && allocated + request.Amount > source!.Value) throw new ConflictException(ResourcesErrorMessages.PEOPLE_ALLOCATION_EXCEEDED);
                var row = new SpecialDrawAllocation
                {
                    BranchId = actor.BranchId,
                    ActorUserId = actor.UserId,
                    CreatedAt = clock.UtcNow(),
                    PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(),
                    Evidence = request.Evidence.Trim(),
                    PreviousId = request.PreviousId,
                    DrawId = request.DrawId,
                    SessionId = request.SessionId,
                    SessionFieldEffectId = source!.Id,
                    SourceRevision = request.Cancelled ? prior!.SourceRevision : source.Revision,
                    SourceValue = request.Cancelled ? prior!.SourceValue : source.Value,
                    SourceDate = request.SourceDate,
                    Amount = request.Amount,
                    Cancelled = request.Cancelled,
                    SignedContribution = (request.Cancelled ? 0 : request.Amount) - (prior?.Amount ?? 0)
                };
                await records.Add(row, token); await unitOfWork.Commit(token); return (row.Id, row.ToResponse());
            }, ct);
    }
}
