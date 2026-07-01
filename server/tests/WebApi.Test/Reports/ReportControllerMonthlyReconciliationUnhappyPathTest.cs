using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
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

    // The route uses bare `:int` constraints, so an out-of-range year reaches
    // MonthlyReconciliationFluentValidation and returns a real 400 ResponseErrorJson — not the
    // body-less framework 404 a min/max route-constraint miss used to produce.
    [Fact]
    public async Task MonthlyReconciliation_ShouldReturn400_WhenYearOutOfRange()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconYear400", Role.Manager);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/1900/8", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);
    }

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturn400_WhenMonthOutOfRange()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconMonth400", Role.Manager);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/2025/13", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_MONTH_INVALID);
    }
}
