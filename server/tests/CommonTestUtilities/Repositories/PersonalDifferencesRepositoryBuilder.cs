using NSubstitute;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace CommonTestUtilities.Repositories;

public static class PersonalDifferencesRepositoryBuilder
{
    public static IPersonalDifferencesRepository Empty()
    {
        var repository = Substitute.For<IPersonalDifferencesRepository>();
        repository.Read(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<Guid?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new PersonalDifferencePage([], 0, []));
        return repository;
    }
}
