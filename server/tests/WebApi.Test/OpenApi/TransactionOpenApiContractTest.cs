using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class TransactionOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldExposeTransactionFilteringIdempotencyAndConcurrencyContracts()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var schemas = root.GetProperty("components").GetProperty("schemas");

        AssertParameter(paths.GetProperty("/transaction").GetProperty("get"), "OriginTransactionId", "query");
        AssertRequiredHeader(paths.GetProperty("/transaction").GetProperty("post"), "Idempotency-Key");
        var installmentCreate = paths.GetProperty("/transaction/installment").GetProperty("post");
        AssertRequiredHeader(installmentCreate, "Idempotency-Key");
        installmentCreate.GetProperty("responses").GetProperty("201").GetProperty("headers")
            .TryGetProperty("ETag", out _).ShouldBeTrue();
        AssertVersionedMutation(paths.GetProperty("/transaction/{transactionId}").GetProperty("put"));
        AssertVersionedMutation(paths.GetProperty("/transaction/{transactionId}/finalize").GetProperty("post"));
        AssertVersionedMutation(paths.GetProperty("/transaction/{transactionId}/cancel").GetProperty("post"));

        AssertProperty(schemas, "ResponseTransactionJson", "version");
        AssertProperty(schemas, "ResponseCreateTransactionJson", "version");
        AssertProperty(schemas, "ResponseCreateTransactionInstallmentJson", "version");
        AssertProperty(schemas, "ResponseListTransactionItemJson", "version");
        AssertProperty(schemas, "ResponseListTransactionItemJson", "originTransactionId");
    }

    private static void AssertVersionedMutation(JsonElement operation)
    {
        AssertRequiredHeader(operation, "If-Match");
        operation.GetProperty("responses").TryGetProperty("400", out _).ShouldBeTrue();
        operation.GetProperty("responses").TryGetProperty("409", out _).ShouldBeTrue();
        operation.GetProperty("responses").GetProperty("200").GetProperty("headers")
            .TryGetProperty("ETag", out _).ShouldBeTrue();
    }

    private static void AssertRequiredHeader(JsonElement operation, string name)
    {
        var parameter = AssertParameter(operation, name, "header");
        parameter.GetProperty("required").GetBoolean().ShouldBeTrue();
    }

    private static JsonElement AssertParameter(JsonElement operation, string name, string location)
    {
        var parameter = operation.GetProperty("parameters")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == name);
        parameter.GetProperty("in").GetString().ShouldBe(location);
        return parameter;
    }

    private static void AssertProperty(JsonElement schemas, string schemaName, string propertyName)
    {
        schemas.GetProperty(schemaName).GetProperty("properties")
            .TryGetProperty(propertyName, out _).ShouldBeTrue();
    }
}
