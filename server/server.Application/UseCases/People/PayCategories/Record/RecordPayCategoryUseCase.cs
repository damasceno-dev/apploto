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

public class RecordPayCategoryUseCase(PeopleCommandRunner runner, IEmployeeRecordsRepository records, IUnitOfWork unitOfWork, IBranchClock clock)
{
    public Task<ResponsePayCategoryJson> Execute(RequestPayCategoryJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new PayCategoryFluentValidation(), request);
        return runner.Run<RequestPayCategoryJson, ResponsePayCategoryJson>(PeopleCommandRunner.Endpoint("pay-category", request.PreviousId), request, key, version, null,
            async (actor, token) =>
            {
                var rows = await records.List<EmployeePayCategory>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<EmployeePayCategory>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                if (prior != null) PeopleHistory.Require(prior.Kind == request.Kind);
                var row = new EmployeePayCategory
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    Name = request.Name.Trim(), Kind = request.Kind, Enabled = request.Enabled,
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
            }, ct);
    }
}
