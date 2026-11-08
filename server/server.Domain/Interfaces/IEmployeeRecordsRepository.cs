using server.Domain.Entities;
namespace server.Domain.Interfaces;

public interface IEmployeeRecordsRepository
{
    Task LockBranch(Guid branchId, CancellationToken ct = default);
    Task Add<T>(T record, CancellationToken ct = default) where T : EntityBase;
    Task<IReadOnlyList<T>> List<T>(Guid branchId, CancellationToken ct = default) where T : EntityBase;
}
