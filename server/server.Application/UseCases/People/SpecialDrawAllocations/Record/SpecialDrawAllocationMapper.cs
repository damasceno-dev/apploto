using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class SpecialDrawAllocationMapper
{
    extension(SpecialDrawAllocation row)
    {
        public ResponseSpecialDrawAllocationJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            DrawId = row.DrawId,
            SessionId = row.SessionId,
            SessionFieldEffectId = row.SessionFieldEffectId,
            SourceRevision = row.SourceRevision,
            SourceValue = row.SourceValue,
            SourceDate = row.SourceDate,
            Amount = row.Amount,
            SignedContribution = row.SignedContribution,
            PreviousId = row.PreviousId,
            Cancelled = row.Cancelled,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
