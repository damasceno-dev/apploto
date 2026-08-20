using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class SettingsRepositoryBuilder
{
    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();

    public SettingsRepositoryBuilder GetByBranchIdReturns(Guid branchId, Setting? result)
    {
        _repository.GetByBranchId(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public SettingsRepositoryBuilder GetByBranchIdAsNoTrackingReturns(Guid branchId, Setting? result)
    {
        _repository.GetByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public ISettingsRepository Build()
    {
        return _repository;
    }
}
