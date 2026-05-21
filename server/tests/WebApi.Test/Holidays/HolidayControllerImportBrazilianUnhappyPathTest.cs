using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerImportBrazilianUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ImportBrazilian_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImport403", Role.Member);

        var httpResponse = await _client.PostAuthAsync("/holiday/import-br/2026", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn200_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewMember", Role.Member);

        var httpResponse = await _client.GetAuthAsync(
            "/holiday/import-br/2026/preview?includeOptionalFederal=true",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayPreviewJson>();
        payload.Items.Count.ShouldBe(13);
    }

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/holiday/import-br/2026/preview");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task ImportBrazilian_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.PostAsync("/holiday/import-br/2026", content: null);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Theory]
    [InlineData("/holiday/import-br/1800/preview")]
    [InlineData("/holiday/import-br/2300")]
    public async Task BrazilianImportRoutes_ShouldReturn404_WhenYearViolatesRouteConstraint(string uri)
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync($"HolBrRoute404{Guid.NewGuid():N}", Role.Admin);

        var httpResponse = uri.EndsWith("/preview", StringComparison.Ordinal)
            ? await _client.GetAuthAsync(uri, token)
            : await _client.PostAuthAsync(uri, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
