using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class CommissionSourceMapper
{
    extension(EmployeeCommissionSource row)
    {
        public ResponseCommissionSourceJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            OperatorId = row.OperatorId,
            SourceDate = row.SourceDate,
            SignedAmount = row.SignedAmount,
            ExternalIdentity = row.ExternalIdentity,
            SourceRole = row.SourceRole,
            PreviousId = row.PreviousId,
            SignedContribution = row.SignedContribution,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
