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

public class RecordCommissionSourceUseCase(PeopleCommandRunner runner, IEmployeeRecordsRepository records, IUnitOfWork unitOfWork, IBranchClock clock)
{
    public Task<ResponseCommissionSourceJson> Execute(RequestCommissionSourceJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new CommissionSourceFluentValidation(), request);
        return runner.Run<RequestCommissionSourceJson, ResponseCommissionSourceJson>(PeopleCommandRunner.Endpoint("commission-source", request.PreviousId), request, key, version, request.OperatorId,
            async (actor, token) =>
            {
                var rows = await records.List<EmployeeCommissionSource>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<EmployeeCommissionSource>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                if (prior != null) PeopleHistory.Require(prior.OperatorId == request.OperatorId && prior.SourceDate == request.SourceDate && prior.ExternalIdentity == request.ExternalIdentity.Trim() && prior.SourceRole == request.SourceRole.Trim());
                else if (rows.Any(r => r.ExternalIdentity == request.ExternalIdentity.Trim())) throw new ConflictException(ResourcesErrorMessages.PEOPLE_DUPLICATE_SOURCE);
                PeopleHistory.Require(request.SourceDate <= request.PostedOn);
                // SourceRole is explicit evidence (e.g. BolaoLeftover / EnteredPrize), never a product-name inference.
                var row = new EmployeeCommissionSource
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    OperatorId = request.OperatorId, SourceDate = request.SourceDate, SignedAmount = request.SignedAmount,
                    SignedContribution = request.SignedAmount - (prior?.SignedAmount ?? 0m), ExternalIdentity = request.ExternalIdentity.Trim(), SourceRole = request.SourceRole.Trim(),
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
            }, ct);
    }
}
