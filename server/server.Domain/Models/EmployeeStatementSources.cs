using server.Domain.Entities.Enums;
namespace server.Domain.Models;

public record EmployeeSessionSource(Guid SessionId, Guid AccountId, Guid? CustodianOperatorId, Guid? RecorderOperatorId, Guid? SubmitterOperatorId, DateTime SourceDate, DailyCloseStatus Status, Guid? OfficialAttemptId, bool HistoryComplete);
public record EmployeeCommissionBasisSource(Guid SourceId, Guid? SessionId, DateTime SourceDate, DateTime PostedOn, CommissionBasisComponent Component, decimal SignedAmount, ClassificationProvenance? Provenance, Guid? TransactionId = null, bool PostingHistoryComplete = true);
public record SpecialFeeSource(Guid Id, Guid SessionId, Guid BranchId, DateTime SourceDate, int Revision, decimal Value, Guid? TransactionId, bool Official, IReadOnlyList<Guid>? TransactionHistory = null);

public record EmployeeCommissionClassificationState(
    Guid TransactionId,
    Guid SessionId,
    DateTime SourceDate,
    DateTime ClassifiedAt,
    bool MatchesPublishedBasis);
