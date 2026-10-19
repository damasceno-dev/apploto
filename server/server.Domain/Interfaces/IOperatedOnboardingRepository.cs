using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatedOnboardingRepository
{
    Task<OperatedOnboarding?> Get(Guid requestId);
    Task Add(OperatedOnboarding record);
}
