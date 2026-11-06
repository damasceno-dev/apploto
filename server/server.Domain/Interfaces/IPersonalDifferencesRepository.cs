using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IPersonalDifferencesRepository
{
    Task<PersonalDifferencePage> Read(Guid branchId, Guid varianceProductId, DateTime from, DateTime to, Guid? operatorId, int page, int pageSize, CancellationToken ct = default);
}
