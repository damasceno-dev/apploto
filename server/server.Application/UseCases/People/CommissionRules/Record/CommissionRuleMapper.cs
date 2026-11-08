using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class CommissionRuleMapper
{
    extension(EmployeeCommissionRule row)
    {
        public ResponseCommissionRuleJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            OperatorId = row.OperatorId,
            EffectiveFrom = row.EffectiveFrom,
            EffectiveTo = row.EffectiveTo,
            RatePercent = row.RatePercent,
            IncludeBolaoFee = row.IncludeBolaoFee,
            IncludeSpecialFee = row.IncludeSpecialFee,
            IncludeExplicitSources = row.IncludeExplicitSources,
            Enabled = row.Enabled,
            PreviousId = row.PreviousId,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
