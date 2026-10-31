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
    public async Task Document_ShouldExposeDailyCloseContracts()
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
        putItemsProperties.TryGetProperty("version", out _).ShouldBeFalse();
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
            schemas.GetProperty("ResponseManagerQueueCloseJson").GetProperty("properties"),
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

        // M9.6 Phase 4: the live variance preview is retired; the calculation is revealed only by Conferir.
        root.GetProperty("paths").TryGetProperty("/session/{sessionId}/variance-preview", out _).ShouldBeFalse();
        schemas.TryGetProperty("RequestDailyCloseVariancePreviewJson", out _).ShouldBeFalse();
        schemas.TryGetProperty("ResponseDailyCloseVariancePreviewJson", out _).ShouldBeFalse();

        foreach (var (path, guarded) in new[]
                 {
                     ("/session/{sessionId}/conferir", true),
                     ("/session/{sessionId}/recount", true),
                     ("/session/{sessionId}/approve", true),
                     ("/session/approve-day", false)
                 })
        {
            var operation = root.GetProperty("paths").GetProperty(path).GetProperty("post");
            var parameters = operation.GetProperty("parameters").EnumerateArray()
                .ToDictionary(parameter => parameter.GetProperty("name").GetString()!, parameter => parameter);
            parameters["Idempotency-Key"].GetProperty("required").GetBoolean().ShouldBeTrue(path);
            parameters.ContainsKey("If-Match").ShouldBe(guarded, path);
            operation.TryGetProperty("requestBody", out _).ShouldBeTrue(path);
        }

        var submitOperation = root.GetProperty("paths").GetProperty("/session/{sessionId}/submit").GetProperty("post");
        submitOperation.TryGetProperty("requestBody", out _).ShouldBeTrue();
        submitOperation.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "If-Match")
            .GetProperty("required").GetBoolean().ShouldBeTrue();

        var countProperties = schemas.GetProperty("ResponseSessionCountJson").GetProperty("properties");
        foreach (var property in new[] { "confirmationToken", "countRevision", "ledgerRevision", "openingRevision", "candidateState", "confirmedAttempt", "unconfirmedRecalculation" })
            countProperties.TryGetProperty(property, out _).ShouldBeTrue(property);
        SchemaTypes(countProperties.GetProperty("confirmedAttempt")).ShouldContain("null");
        var candidateStates = schemas.GetProperty("SessionCandidateState").GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString()).ToList();
        candidateStates.ShouldBe(["NotConferred", "Current", "Outdated", "Unconfirmed", "Recounting"]);

        // The account/day open is retired: custody starts through the acknowledged start (M9.6 Phase 2–3).
        root.GetProperty("paths").TryGetProperty("/dailyclose", out _).ShouldBeFalse();
        var startOperation = root.GetProperty("paths")
            .GetProperty("/session/start")
            .GetProperty("post");
        startOperation.GetProperty("responses").GetProperty("201")
            .GetProperty("headers").TryGetProperty("ETag", out _).ShouldBeTrue();
        startOperation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key")
            .GetProperty("required").GetBoolean().ShouldBeTrue();
        foreach (var path in new[]
                 {
                     "/session/opening-proposal",
                     "/session/{sessionId}/handover",
                     "/session/handover/{handoverId}/accept",
                     "/session/handover/{handoverId}/dispute",
                     "/session/handover/{handoverId}/withdraw",
                     "/session/{sessionId}/finish",
                     "/session/{sessionId}/custody-exception",
                     "/session/declare",
                     "/session/{sessionId}/rearrange",
                     "/session/opening-discrepancy/{discrepancyId}/attribution",
                     "/session/opening-count"
                 })
        {
            root.GetProperty("paths").TryGetProperty(path, out _).ShouldBeTrue(path);
        }

        var putItemsOperation = root.GetProperty("paths")
            .GetProperty("/session/{sessionId}/items")
            .GetProperty("put");
        var ifMatch = putItemsOperation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "If-Match");
        ifMatch.GetProperty("in").GetString().ShouldBe("header");
        ifMatch.GetProperty("required").GetBoolean().ShouldBeTrue();
        putItemsOperation.GetProperty("responses").GetProperty("200")
            .GetProperty("headers").TryGetProperty("ETag", out _).ShouldBeTrue();

        AssertWorkflowEndpoint(
            root,
            "/session/{sessionId}/recall",
            ["200", "401", "403", "404", "409"]);
        AssertWorkflowEndpoint(
            root,
            "/session/{sessionId}/reopen",
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
