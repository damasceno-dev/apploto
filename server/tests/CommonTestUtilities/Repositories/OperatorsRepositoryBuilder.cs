using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class OperatorsRepositoryBuilder
{
    private readonly IOperatorsRepository _repository = Substitute.For<IOperatorsRepository>();

    public OperatorsRepositoryBuilder GetActiveByIdAndBranchId(Operator? op)
    {
        _repository.GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(op);
        return this;
    }

    public OperatorsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Operator? op)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(op);
        return this;
    }

    public OperatorsRepositoryBuilder ListActiveByBranchId(IReadOnlyList<Operator> operators)
    {
        _repository.ListActiveByBranchId(Arg.Any<Guid>()).Returns(operators);
        return this;
    }

    public IOperatorsRepository Build()
    {
        return _repository;
    }
}
