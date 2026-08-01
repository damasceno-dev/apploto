using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public class DailyCloseOpenApiContractTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Document_ShouldExposeM7_7Phase2DailyCloseContract()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var reviewItem = schemas
            .GetProperty("ResponseDailyCloseReviewItemJson")
            .GetProperty("properties");

        SchemaTypes(reviewItem.GetProperty("closingValue"))
            .ShouldContain("null");
        SchemaTypes(reviewItem.GetProperty("openingValue"))
            .ShouldContain("null");
        SchemaTypes(reviewItem.GetProperty("displayOrder"))
            .ShouldNotContain("null");

        var putItemsProperties = schemas
            .GetProperty("RequestPutDailyCloseItemsJson")
            .GetProperty("properties");
        var version = putItemsProperties.GetProperty("version");
        SchemaTypes(version).ShouldNotContain("null");
        version.GetProperty("format").GetString().ShouldBe("uint32");
        SchemaTypes(putItemsProperties.GetProperty("notes")).ShouldContain("null");

        var previewRequestProperties = schemas
            .GetProperty("RequestDailyCloseVariancePreviewJson")
            .GetProperty("properties");
        previewRequestProperties.TryGetProperty("items", out _).ShouldBeTrue();
        var previewResponseProperties = schemas
            .GetProperty("ResponseDailyCloseVariancePreviewJson")
            .GetProperty("properties");
        SchemaTypes(previewResponseProperties.GetProperty("cashVariance"))
            .ShouldNotContain("null");

        var previewPath = root.GetProperty("paths")
            .GetProperty("/dailyclose/{dailyCloseId}/variance-preview")
            .GetProperty("post");
        previewPath.TryGetProperty("requestBody", out _).ShouldBeTrue();
        previewPath.GetProperty("responses").TryGetProperty("200", out _).ShouldBeTrue();
    }

    private static IReadOnlyList<string> SchemaTypes(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()!).ToList()
            : [type.GetString()!];
    }
}
