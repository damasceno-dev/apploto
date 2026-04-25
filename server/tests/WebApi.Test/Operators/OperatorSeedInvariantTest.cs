using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Operators;

[Collection(ServerApiCollection.Name)]
public class OperatorSeedInvariantTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task SeedOperatorAsync_ShouldThrowFixtureSetupException_WhenActiveUserLinkAlreadySeeded()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("OpSeedDuplicateInvariant");
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            factory.SeedOperatorAsync(branch.Id, userId: user.Id));

        exception.Message.ShouldContain("SeedOperatorAsync fixture setup violated the active operator user-link invariant");
    }
}
