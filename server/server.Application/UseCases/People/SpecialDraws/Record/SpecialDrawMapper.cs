using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class SpecialDrawMapper
{
    extension(SpecialDraw row)
    {
        public ResponseSpecialDrawJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            Name = row.Name,
            Modality = row.Modality,
            Contest = row.Contest,
            PeriodFrom = row.PeriodFrom,
            PeriodTo = row.PeriodTo,
            Prize = row.Prize,
            Enabled = row.Enabled,
            PreviousId = row.PreviousId,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
