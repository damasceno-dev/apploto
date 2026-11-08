using server.Domain.Models;
namespace server.Domain.Interfaces;

public interface IEmployeeStatementSources
{
    Task<IReadOnlyList<EmployeeSessionSource>> Sessions(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeCommissionBasisSource>> CommissionBasis(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeSessionSource>> UnresolvedCustodySessions(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeCommissionClassificationState>> CommissionClassificationStates(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
    Task CaptureCommissionBasis(Guid branchId, Guid sessionId, DateTime postedOn, DateTime recordedAt, CancellationToken ct = default);
    Task LockCommissionAccounts(Guid branchId, CancellationToken ct = default);
    Task LockSpecialFee(Guid branchId, Guid effectId, CancellationToken ct = default);
    Task<IReadOnlyList<SpecialFeeSource>> SpecialFees(
        Guid branchId,
        Guid? operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default,
        IReadOnlyCollection<Guid>? includeEffects = null,
        IReadOnlyCollection<Guid>? includeSessions = null);
    Task<SpecialFeeSource?> SpecialFee(Guid branchId, Guid effectId, CancellationToken ct = default);
}
