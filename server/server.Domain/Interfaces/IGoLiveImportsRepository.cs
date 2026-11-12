using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IGoLiveImportsRepository
{
    Task<IReadOnlyList<GoLiveImportTarget>> Targets(Guid branchId, Guid importId, CancellationToken ct);
    Task LockTargets(Guid branchId, CancellationToken ct);
    Task<DateTime?> ActivatedLockDate(Guid branchId, CancellationToken ct);
    Task Audit(GoLiveImportAudit audit, CancellationToken ct);
    Task Register(GoLiveImportTarget target, CancellationToken ct);
    Task<bool> HasActivated(Guid branchId, CancellationToken ct);
    Task Add(GoLiveImport import, CancellationToken ct);
    Task<GoLiveImport?> GetForUpdate(Guid branchId, Guid id, CancellationToken ct);
    Task<GoLiveImport?> Get(Guid branchId, Guid id, CancellationToken ct);
    Task<GoLiveImport?> FindDigest(Guid branchId, string digest, CancellationToken ct);
    Task<IReadOnlyList<GoLiveImport>> List(Guid branchId, CancellationToken ct);
    Task<string> Fingerprint(Guid branchId, CancellationToken ct);
}
