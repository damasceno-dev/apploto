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

public class RecordSpecialDrawUseCase(PeopleCommandRunner runner, IEmployeeRecordsRepository records, IUnitOfWork unitOfWork, IBranchClock clock)
{
    public Task<ResponseSpecialDrawJson> Execute(RequestSpecialDrawJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new SpecialDrawFluentValidation(), request);
        return runner.Run<RequestSpecialDrawJson, ResponseSpecialDrawJson>(PeopleCommandRunner.Endpoint("special-draw", request.PreviousId), request, key, version, null,
            async (actor, token) =>
            {
                var rows = await records.List<SpecialDraw>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<SpecialDraw>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                if (prior != null)
                {
                    var allocations = await records.List<SpecialDrawAllocation>(actor.BranchId, token);
                    // Historical identity stays in old revisions; an allocated/locked catalog row can only be retired.
                    var chainIds = PeopleHistory.Ancestors(rows, prior.Id, r => r.PreviousId).Select(r => r.Id).ToHashSet();
                    if (allocations.Any(a => chainIds.Contains(a.DrawId) && a.SourceDate <= locked))
                        PeopleHistory.Require(!request.Enabled && request.Name.Trim() == prior.Name && request.Modality.Trim() == prior.Modality && request.Contest.Trim() == prior.Contest && request.PeriodFrom == prior.PeriodFrom && request.PeriodTo == prior.PeriodTo && request.Prize == prior.Prize);
                }
                var row = new SpecialDraw
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    Name = request.Name.Trim(), Modality = request.Modality.Trim(), Contest = request.Contest.Trim(),
                    PeriodFrom = request.PeriodFrom, PeriodTo = request.PeriodTo, Prize = request.Prize, Enabled = request.Enabled,
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
            }, ct);
    }
}
