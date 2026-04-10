using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class SettingsRepositoryBuilder
{
    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();

    public ISettingsRepository Build()
    {
        return _repository;
    }
}
