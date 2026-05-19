using NSubstitute;
using server.Application.Services.DailyCloses;

namespace CommonTestUtilities.Services;

public class CashVarianceProductResolverBuilder
{
    private readonly ICashVarianceProductResolver _resolver = Substitute.For<ICashVarianceProductResolver>();

    public CashVarianceProductResolverBuilder ReturnsId(Guid branchId, Guid productId)
    {
        _resolver.GetIdAsync(branchId, Arg.Any<CancellationToken>()).Returns(productId);
        return this;
    }

    public ICashVarianceProductResolver Build()
    {
        return _resolver;
    }
}
