using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class OperatorsRepositoryBuilder
{
    private readonly IOperatorsRepository _repository = Substitute.For<IOperatorsRepository>();

    public OperatorsRepositoryBuilder GetActiveByIdAndBranchId(Guid id, Guid branchId, Operator? op)
    {
        _repository.GetActiveByIdAndBranchId(id, branchId).Returns(op);
        return this;
    }

    public OperatorsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, Operator? op)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(id, branchId).Returns(op);
        return this;
    }

    public OperatorsRepositoryBuilder ListActiveByBranchId(Guid branchId, IReadOnlyList<Operator> operators)
    {
        _repository.ListActiveByBranchId(branchId).Returns(operators);
        return this;
    }

    public OperatorsRepositoryBuilder GetActiveByUserIdAndBranchId(Guid userId, Guid branchId, Operator? op)
    {
        _repository.GetActiveByUserIdAndBranchId(userId, branchId).Returns(op);
        return this;
    }

    public IOperatorsRepository Build()
    {
        return _repository;
    }
}
