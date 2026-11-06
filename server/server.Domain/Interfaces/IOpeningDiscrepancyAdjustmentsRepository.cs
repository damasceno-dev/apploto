using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOpeningDiscrepancyAdjustmentsRepository
{
    Task<SessionOpeningDiscrepancy?> LockSource(Guid branchId, Guid discrepancyId, CancellationToken ct);
    Task<OpeningDiscrepancyAdjustment?> Latest(Guid branchId, Guid discrepancyId, CancellationToken ct);
    Task<IReadOnlyList<OpeningDiscrepancyAdjustment>> List(Guid branchId, Guid discrepancyId, int afterSequence, int take, CancellationToken ct);
    Task Add(OpeningDiscrepancyAdjustment adjustment, CancellationToken ct);
}
