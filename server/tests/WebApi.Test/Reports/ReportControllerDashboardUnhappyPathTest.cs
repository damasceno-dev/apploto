using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerDashboardUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/report/dashboard?date=2025-03-10");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role cannot access the dashboard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DashUnhappy403", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-10", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 400 — default/invalid date
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldReturn400_WhenDateIsDefault()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashUnhappy400Default", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=0001-01-01", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }

    [Fact]
    public async Task Dashboard_ShouldReturn400_WhenDateIsMissing()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashUnhappy400Missing", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var httpResponse = await _client.GetAuthAsync("/report/dashboard", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
