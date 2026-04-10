using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class BranchesRepositoryBuilder
{
    private readonly IBranchesRepository _repository = Substitute.For<IBranchesRepository>();

    public BranchesRepositoryBuilder GetById(Branch? branch)
    {
        _repository.GetById(Arg.Any<Guid>()).Returns(branch);
        return this;
    }

    public IBranchesRepository Build()
    {
        return _repository;
    }
}
