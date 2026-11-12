using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IGoLiveHistoryRepository
{
    Task Add<T>(T row, CancellationToken ct) where T : EntityBase;
    Task<bool> HasCashboxHistory(Guid branchId, Guid accountId, CancellationToken ct);
}
