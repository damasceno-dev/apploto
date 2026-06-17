using System.Net;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerMonthlyReconciliationUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/report/monthly-reconciliation/2025/8");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconMember", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/2025/8", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturn404_WhenYearViolatesRouteConstraint()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconYear404", Role.Manager);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/1900/8", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
