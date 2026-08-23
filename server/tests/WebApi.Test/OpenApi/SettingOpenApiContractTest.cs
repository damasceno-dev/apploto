using System.Net;
using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class SettingOpenApiContractTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Document_ShouldExposeSettingMutationContracts()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var requestProperties = schemas
            .GetProperty("RequestLockSettingMonthJson")
            .GetProperty("properties");
        requestProperties.TryGetProperty("year", out _).ShouldBeTrue();
        requestProperties.TryGetProperty("month", out _).ShouldBeTrue();

        var paths = root.GetProperty("paths");
        var post = paths
            .GetProperty("/setting/lock-month")
            .GetProperty("post");
        post.TryGetProperty("requestBody", out _).ShouldBeTrue();
        var responses = post.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "409" })
            responses.TryGetProperty(status, out _).ShouldBeTrue($"lock-month must declare HTTP {status}");

        var settingProperties = schemas
            .GetProperty("ResponseSettingJson")
            .GetProperty("properties");
        settingProperties.TryGetProperty("lockDate", out _).ShouldBeTrue();
        settingProperties.TryGetProperty("version", out _).ShouldBeTrue();

        AssertVersionedSettingMutation(post);
        AssertVersionedSettingMutation(paths.GetProperty("/setting").GetProperty("put"));
    }

    private static void AssertVersionedSettingMutation(JsonElement operation)
    {
        var ifMatch = operation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "If-Match");
        ifMatch.GetProperty("required").GetBoolean().ShouldBeTrue();
        var responses = operation.GetProperty("responses");
        responses.TryGetProperty("409", out _).ShouldBeTrue();
        responses.GetProperty("200").GetProperty("headers")
            .TryGetProperty("ETag", out _).ShouldBeTrue();
    }
}
