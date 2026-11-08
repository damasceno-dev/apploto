using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class HourBankEntryMapper
{
    extension(HourBankEntry row)
    {
        public ResponseHourBankEntryJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            OperatorId = row.OperatorId,
            Kind = row.Kind,
            SourceDate = row.SourceDate,
            SignedHours = row.SignedHours,
            SignedContribution = row.SignedContribution,
            PreviousId = row.PreviousId,
            Cancelled = row.Cancelled,
            ExternalIdentity = row.ExternalIdentity,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
