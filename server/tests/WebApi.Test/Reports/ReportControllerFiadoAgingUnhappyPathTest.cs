using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoAgingUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FiadoAging_ShouldReturn401_WhenTokenIsMissing()
    {
        const string url = "/report/fiado/aging?page=1&pageSize=50";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task FiadoAging_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FAUnhappy403", Role.Member);
        const string url = "/report/fiado/aging?page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FiadoAging_ShouldReturn400_WhenPageIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FAUnhappy400Page");
        const string url = "/report/fiado/aging?page=0&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public async Task FiadoAging_ShouldReturn400_WhenPageSizeIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FAUnhappy400PageSize");
        const string url = "/report/fiado/aging?page=1&pageSize=0";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public async Task FiadoAging_ShouldReturn400_WhenAsOfDateIsDefault()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FAUnhappy400AsOf");
        const string url = "/report/fiado/aging?asOfDate=0001-01-01&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
