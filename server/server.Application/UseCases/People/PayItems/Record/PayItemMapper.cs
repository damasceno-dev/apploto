using server.Domain.Entities;
using server.Communication.Responses;
namespace server.Application.UseCases.People;

public static class PayItemMapper
{
    extension(EmployeePayItem row)
    {
        public ResponsePayItemJson ToResponse() => new()
        {
            BranchId = row.BranchId,
            ActorUserId = row.ActorUserId,
            PostedOn = row.PostedOn,
            Reason = row.Reason,
            Evidence = row.Evidence,
            Version = row.Version,
            OperatorId = row.OperatorId,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            SourceMonth = row.SourceMonth,
            SourceDate = row.SourceDate,
            Amount = row.Amount,
            Kind = row.Kind,
            DeductionPolicy = row.DeductionPolicy,
            PreviousId = row.PreviousId,
            Cancelled = row.Cancelled,
            SignedContribution = row.SignedContribution,
            CommissionCalculationId = row.CommissionCalculationId,
            CommissionEvidenceJson = row.CommissionEvidenceJson,
            Id = row.Id,
            CreatedAt = row.CreatedAt,
        };
    }
}
