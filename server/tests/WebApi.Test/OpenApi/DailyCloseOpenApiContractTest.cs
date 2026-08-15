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
    public async Task Document_ShouldExposeM7_7Phase3DailyCloseContract()
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
        AssertSchemaIsNonNullable(reviewItem.GetProperty("displayOrder"));

        var putItemsProperties = schemas
            .GetProperty("RequestPutDailyCloseItemsJson")
            .GetProperty("properties");
        var version = putItemsProperties.GetProperty("version");
        AssertSchemaIsNonNullable(version);
        version.GetProperty("format").GetString().ShouldBe("uint32");
        SchemaTypes(putItemsProperties.GetProperty("notes")).ShouldContain("null");
        var closeResponseProperties = schemas
            .GetProperty("ResponseDailyCloseJson")
            .GetProperty("properties");
        AssertOwnershipProperties(closeResponseProperties, includesOpener: true);
        closeResponseProperties.TryGetProperty("itemsFirstRecordedAt", out _).ShouldBeTrue();
        closeResponseProperties.TryGetProperty("openingRecheckRequiredAt", out _).ShouldBeTrue();
        closeResponseProperties.TryGetProperty("openingRecheckTriggeredByDailyCloseId", out _).ShouldBeTrue();
        closeResponseProperties.TryGetProperty("openingRecheckTriggeredByUserId", out _).ShouldBeTrue();
        var reviewResponseProperties = schemas
            .GetProperty("ResponseDailyCloseReviewJson")
            .GetProperty("properties");
        AssertOwnershipProperties(reviewResponseProperties, includesOpener: true);
        reviewResponseProperties.TryGetProperty("itemsFirstRecordedAt", out _).ShouldBeTrue();
        reviewResponseProperties.TryGetProperty("openingRecheckRequiredAt", out _).ShouldBeTrue();
        reviewResponseProperties.TryGetProperty("openingRecheckTriggeredByDailyCloseId", out _).ShouldBeTrue();
        reviewResponseProperties.TryGetProperty("openingRecheckTriggeredByUserId", out _).ShouldBeTrue();

        AssertOwnershipProperties(
            schemas.GetProperty("ResponseListDailyCloseItemJson").GetProperty("properties"),
            includesOpener: false);
        AssertOwnershipProperties(
            schemas.GetProperty("ResponseDashboardCloseJson").GetProperty("properties"),
            includesOpener: false);

        var putResponseProperties = schemas
            .GetProperty("ResponsePutDailyCloseItemsJson")
            .GetProperty("properties");
        putResponseProperties.TryGetProperty("dailyClose", out _).ShouldBeTrue();
        SchemaTypes(putResponseProperties.GetProperty("affectedSuccessor")).ShouldContain("null");
        var affectedProperties = schemas
            .GetProperty("ResponseAffectedDailyCloseSuccessorJson")
            .GetProperty("properties");
        foreach (var property in new[]
                 {
                     "dailyCloseId",
                     "date",
                     "previousStatus",
                     "newStatus",
                     "openingRecheckRequiredAt"
                 })
        {
            affectedProperties.TryGetProperty(property, out _).ShouldBeTrue();
        }

        var previewRequestProperties = schemas
            .GetProperty("RequestDailyCloseVariancePreviewJson")
            .GetProperty("properties");
        previewRequestProperties.TryGetProperty("items", out _).ShouldBeTrue();
        var previewResponseProperties = schemas
            .GetProperty("ResponseDailyCloseVariancePreviewJson")
            .GetProperty("properties");
        AssertSchemaIsNonNullable(previewResponseProperties.GetProperty("cashVariance"));

        var previewPath = root.GetProperty("paths")
            .GetProperty("/dailyclose/{dailyCloseId}/variance-preview")
            .GetProperty("post");
        previewPath.TryGetProperty("requestBody", out _).ShouldBeTrue();
        previewPath.GetProperty("responses").TryGetProperty("200", out _).ShouldBeTrue();

        AssertWorkflowEndpoint(
            root,
            "/dailyclose/{dailyCloseId}/recall",
            ["200", "401", "403", "404", "409"]);
        AssertWorkflowEndpoint(
            root,
            "/dailyclose/{dailyCloseId}/reopen",
            ["200", "401", "403", "404", "409"]);
    }

    private static void AssertWorkflowEndpoint(
        JsonElement root,
        string path,
        IReadOnlyList<string> expectedStatuses)
    {
        var post = root.GetProperty("paths").GetProperty(path).GetProperty("post");
        post.TryGetProperty("requestBody", out _).ShouldBeFalse();
        var responses = post.GetProperty("responses");
        foreach (var status in expectedStatuses)
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue(
                $"{path} must declare HTTP {status}");
        }
    }

    private static void AssertOwnershipProperties(JsonElement properties, bool includesOpener)
    {
        var expected = new List<string>
        {
            "recordedByUserId",
            "recordedByUserName",
            "recordedByOperatorId",
            "recordedByOperatorName",
            "submittedByUserId",
            "submittedByUserName",
            "submittedByOperatorId",
            "submittedByOperatorName"
        };
        if (includesOpener)
        {
            expected.Insert(0, "openedByUserId");
            expected.Insert(1, "openedByUserName");
        }

        foreach (var property in expected)
            properties.TryGetProperty(property, out _).ShouldBeTrue();
    }

    private static IReadOnlyList<string> SchemaTypes(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var type))
        {
            return type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(value => value.GetString()!).ToList()
                : [type.GetString()!];
        }

        foreach (var composition in new[] { "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(composition, out var alternatives))
                return alternatives.EnumerateArray().SelectMany(SchemaTypes).ToList();
        }

        return [];
    }

    private static void AssertSchemaIsNonNullable(JsonElement schema)
    {
        var types = SchemaTypes(schema);
        types.ShouldNotBeEmpty("a non-nullable schema must declare at least one concrete type");
        types.ShouldNotContain("null");
    }
}
