using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseAccountCoordinationKeyTest
{
    [Fact]
    public void Compute_ShouldUsePinnedSha256BigEndianVector()
    {
        var branchId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var accountId = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");

        DailyCloseAccountCoordinationKey.Compute(branchId, accountId)
            .ShouldBe(4_327_585_769_618_283_591L);
    }
}
