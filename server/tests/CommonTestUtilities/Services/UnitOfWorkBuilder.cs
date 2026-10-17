using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public class UnitOfWorkBuilder
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public UnitOfWorkBuilder()
    {
        _unitOfWork.TryCommitOptimistic(Arg.Any<CancellationToken>()).Returns(true);
    }

    public UnitOfWorkBuilder TryCommitOptimisticConflicts()
    {
        _unitOfWork.TryCommitOptimistic(Arg.Any<CancellationToken>()).Returns(false);
        return this;
    }

    public IUnitOfWork Build()
    {
        return _unitOfWork;
    }
}
