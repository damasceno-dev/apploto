using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Settings;

public sealed class MonthLockCoordinationKeyTest
{
    [Fact]
    public void Compute_ShouldUsePinnedNamespacedSha256BigEndianVector()
    {
        var branchId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        MonthLockCoordinationKey.Compute(branchId).ShouldBe(-439_312_244_919_446_065L);
    }
}
