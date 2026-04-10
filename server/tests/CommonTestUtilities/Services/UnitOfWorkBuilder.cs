using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public class UnitOfWorkBuilder
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public IUnitOfWork Build()
    {
        return _unitOfWork;
    }
}
