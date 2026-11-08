using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class PayCategoryMapper
{
    extension(EmployeePayCategory row)
    {
        public ResponsePayCategoryJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            Name = row.Name,
            Kind = row.Kind,
            Enabled = row.Enabled,
            PreviousId = row.PreviousId,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
