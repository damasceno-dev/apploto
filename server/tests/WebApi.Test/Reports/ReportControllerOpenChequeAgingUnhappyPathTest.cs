using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerOpenChequeAgingUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturn401_WhenTokenIsMissing()
    {
        const string url = "/report/cheques/open-aging?page=1&pageSize=50";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role cannot access open-cheque aging
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OCAUnhappy403", Role.Member);
        const string url = "/report/cheques/open-aging?page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 400 — validation failures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturn400_WhenPageIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OCAUnhappy400Page");
        const string url = "/report/cheques/open-aging?page=0&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public async Task OpenChequeAging_ShouldReturn400_WhenPageSizeIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OCAUnhappy400PageSize");
        const string url = "/report/cheques/open-aging?page=1&pageSize=0";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public async Task OpenChequeAging_ShouldReturn400_WhenAsOfDateIsDefault()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OCAUnhappy400AsOfDate");
        const string url = "/report/cheques/open-aging?asOfDate=0001-01-01&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
