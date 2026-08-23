using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class TimeEntryOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldExposeInProgressListFilters()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/timeentry")
            .GetProperty("get");

        AssertQueryParameter(operation, "IsInProgress");
        AssertQueryParameter(operation, "InProgressFirst");
    }

    private static void AssertQueryParameter(JsonElement operation, string name)
    {
        var parameter = operation.GetProperty("parameters")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == name);
        parameter.GetProperty("in").GetString().ShouldBe("query");
    }
}
