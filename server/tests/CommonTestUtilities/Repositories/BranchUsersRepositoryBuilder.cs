using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class BranchUsersRepositoryBuilder
{
    private readonly IBranchUsersRepository _repository = Substitute.For<IBranchUsersRepository>();

    public BranchUsersRepositoryBuilder GetById(Guid branchUserId, BranchUser? branchUser)
    {
        _repository.GetById(branchUserId).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder GetActiveById(Guid branchUserId, BranchUser? branchUser)
    {
        _repository.GetActiveById(branchUserId).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder GetActiveByUserIdAndBranchId(Guid userId, Guid branchId, BranchUser? branchUser)
    {
        _repository.GetActiveByUserIdAndBranchId(userId, branchId).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder GetByUserIdAndBranchId(Guid userId, Guid branchId, BranchUser? branchUser)
    {
        _repository.GetByUserIdAndBranchId(userId, branchId).Returns(branchUser);
        return this;
    }

    public BranchUsersRepositoryBuilder CountActiveAdminsByBranchId(Guid branchId, int count)
    {
        _repository.CountActiveAdminsByBranchId(branchId).Returns(count);
        return this;
    }

    public BranchUsersRepositoryBuilder ListActiveByBranchId(Guid branchId, IReadOnlyList<BranchUser> branchUsers)
    {
        _repository.ListActiveByBranchId(branchId).Returns(branchUsers);
        return this;
    }

    public BranchUsersRepositoryBuilder ListActiveByUserId(Guid userId, IReadOnlyList<BranchUser> branchUsers)
    {
        _repository.ListActiveByUserId(userId).Returns(branchUsers);
        return this;
    }

    public IBranchUsersRepository Build()
    {
        return _repository;
    }
}
