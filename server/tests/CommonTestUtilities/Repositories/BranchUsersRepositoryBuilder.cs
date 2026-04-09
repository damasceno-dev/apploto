using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class BranchUsersRepositoryBuilder
{
    private readonly IBranchUsersRepository _repository = Substitute.For<IBranchUsersRepository>();

    public BranchUsersRepositoryBuilder GetActiveById(BranchUser? branchUser)
    {
        _repository.GetActiveById(Arg.Any<Guid>()).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder GetActiveByUserIdAndBranchId(BranchUser? branchUser)
    {
        _repository.GetActiveByUserIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder GetByUserIdAndBranchId(BranchUser? branchUser)
    {
        _repository.GetByUserIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(branchUser);
        return this;
    }

    public IBranchUsersRepository Build()
    {
        return _repository;
    }
}
