using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoBalanceUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FiadoBalance_ShouldReturn401_WhenTokenIsMissing()
    {
        const string url = "/report/fiado/balance?asOfDate=2025-06-30";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task FiadoBalance_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FBUnhappy403", Role.Member);
        const string url = "/report/fiado/balance?asOfDate=2025-06-30";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The action declares [ProducesResponseType(400)] for the
    // FiadoBalanceFluentValidation AsOfDate rule, but no WebApi test exercised it — the
    // sibling asOfDate reports (fiado/aging, cheques/open-aging) already pin this path.
    [Fact]
    public async Task FiadoBalance_ShouldReturn400_WhenAsOfDateIsDefault()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FBUnhappy400AsOfDate");
        const string url = "/report/fiado/balance?asOfDate=0001-01-01";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
