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

public class RecordCommissionRuleUseCase(PeopleCommandRunner runner, IEmployeeRecordsRepository records, IUnitOfWork unitOfWork, IBranchClock clock)
{
    public Task<ResponseCommissionRuleJson> Execute(RequestCommissionRuleJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new CommissionRuleFluentValidation(), request);
        return runner.Run<RequestCommissionRuleJson, ResponseCommissionRuleJson>(PeopleCommandRunner.Endpoint("commission-rule", request.PreviousId), request, key, version, request.OperatorId,
            async (actor, token) =>
            {
                var rows = await records.List<EmployeeCommissionRule>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<EmployeeCommissionRule>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                if (prior != null && prior.EffectiveFrom <= locked)
                {
                    // An already effective rule may only surrender an open future tail, preserving every locked date.
                    PeopleHistory.Require(prior.OperatorId == request.OperatorId && prior.EffectiveFrom == request.EffectiveFrom &&
                        request.EffectiveTo < prior.EffectiveTo && request.EffectiveTo >= locked &&
                        prior.RatePercent == request.RatePercent && prior.IncludeBolaoFee == request.IncludeBolaoFee &&
                        prior.IncludeSpecialFee == request.IncludeSpecialFee && prior.IncludeExplicitSources == request.IncludeExplicitSources &&
                        prior.Enabled == request.Enabled);
                }
                else
                {
                    PeopleHistory.Open(request.EffectiveFrom, locked);
                    if (prior != null) PeopleHistory.Require(prior.OperatorId == request.OperatorId && prior.EffectiveFrom == request.EffectiveFrom && request.EffectiveTo <= prior.EffectiveTo);
                }
                if (request.Enabled && PeopleHistory.Heads(rows, r => r.PreviousId).Any(r => r.Id != prior?.Id && r.Enabled && r.OperatorId == request.OperatorId && r.EffectiveFrom <= request.EffectiveTo && r.EffectiveTo >= request.EffectiveFrom))
                    throw new ConflictException(ResourcesErrorMessages.PEOPLE_RULE_OVERLAP);
                var row = new EmployeeCommissionRule
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    OperatorId = request.OperatorId, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
                    RatePercent = request.RatePercent, IncludeBolaoFee = request.IncludeBolaoFee, IncludeSpecialFee = request.IncludeSpecialFee,
                    IncludeExplicitSources = request.IncludeExplicitSources, Enabled = request.Enabled,
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
            }, ct);
    }
}
