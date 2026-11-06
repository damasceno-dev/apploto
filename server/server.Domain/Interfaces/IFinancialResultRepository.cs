using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IFinancialResultRepository
{
    Task<FinancialResultPage> Read(Guid branchId, Guid varianceProductId, DateTime from, DateTime to, FinancialClass? financialClass, int page, int pageSize, CancellationToken ct = default);
}
