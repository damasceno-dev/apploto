using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class CashVarianceOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldExposeDailyCloseIdOnSummaryRows()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var properties = document.RootElement.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ResponseCashVarianceSummaryItemJson")
            .GetProperty("properties");

        properties.TryGetProperty("dailyCloseId", out _).ShouldBeTrue();
    }
}
