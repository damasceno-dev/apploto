using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M11 A19 server-side generation guard: the Dashboard's API is the management overview, never a <c>dashboard</c> path
/// or schema; the Manager Work Queue keeps its own route and response; the new M11 report operations are published.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class ManagementOverviewOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldHaveNoDashboardSurface_AndKeepTheWorkQueue()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Select(schema => schema.Name).ToList();

        paths.ShouldNotContain(path => path.Contains("dashboard", StringComparison.OrdinalIgnoreCase));
        schemas.ShouldNotContain(schema => schema.Contains("dashboard", StringComparison.OrdinalIgnoreCase));

        paths.ShouldContain("/report/manager-queue");
        paths.ShouldContain("/report/management-overview");
        paths.ShouldContain("/report/upcoming-settlements");
        paths.ShouldContain("/report/monthly-financial/{year}/{month}");
        ResponseRef(document, "/report/manager-queue").ShouldBe("#/components/schemas/ResponseManagerQueueJson");
        ResponseRef(document, "/report/management-overview").ShouldBe("#/components/schemas/ResponseManagementOverviewJson");

        // Every section status is the named string enum, never a number.
        var sectionStatus = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("OverviewSectionStatus");
        sectionStatus.GetProperty("enum").EnumerateArray().Select(value => value.GetString())
            .ShouldBe(["Ready", "Empty", "NotConfigured"]);
    }

    private static string? ResponseRef(JsonDocument document, string path)
    {
        return document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("$ref").GetString();
    }
}
