using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class DashboardOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldExposeBacklogAndUnlockedDraftTransactionCountNullability()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var dashboard = schemas.GetProperty("ResponseDashboardJson").GetProperty("properties");
        var backlog = schemas.GetProperty("ResponseDashboardBacklogJson").GetProperty("properties");

        // A plain component reference: the backlog object is always present, never null.
        dashboard.GetProperty("backlog").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/ResponseDashboardBacklogJson");
        AssertNonNullableInteger(dashboard.GetProperty("unlockedDraftTransactionCount"));

        backlog.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ShouldBe(
            ["draftCount", "oldestPendingDate", "rejectedCount", "submittedCount"]);
        AssertNonNullableInteger(backlog.GetProperty("submittedCount"));
        AssertNonNullableInteger(backlog.GetProperty("rejectedCount"));
        AssertNonNullableInteger(backlog.GetProperty("draftCount"));

        var oldestPendingDate = backlog.GetProperty("oldestPendingDate");
        var oldestPendingDateTypes = TypeNames(oldestPendingDate);
        oldestPendingDateTypes.ShouldContain("string");
        oldestPendingDateTypes.ShouldContain("null");
        oldestPendingDate.GetProperty("format").GetString().ShouldBe("date-time");
    }

    private static void AssertNonNullableInteger(JsonElement property)
    {
        var types = TypeNames(property);
        types.ShouldContain("integer");
        types.ShouldNotContain("null");
        property.GetProperty("format").GetString().ShouldBe("int32");
    }

    private static List<string> TypeNames(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()!).ToList()
            : [type.GetString()!];
    }
}
