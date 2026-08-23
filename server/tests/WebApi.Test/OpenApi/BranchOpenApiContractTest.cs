using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class BranchOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldExposeAuthoritativeLocalDateAndTime()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var properties = document.RootElement.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ResponseGetCurrentBranchSummaryJson")
            .GetProperty("properties");

        AssertStringFormat(properties.GetProperty("branchLocalDate"), "date");
        AssertStringFormat(properties.GetProperty("branchLocalDateTime"), "date-time");
    }

    private static void AssertStringFormat(JsonElement property, string expectedFormat)
    {
        property.GetProperty("type").GetString().ShouldBe("string");
        property.GetProperty("format").GetString().ShouldBe(expectedFormat);
    }
}
