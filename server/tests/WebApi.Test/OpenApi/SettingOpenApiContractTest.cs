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
    public async Task Document_ShouldExposeM7_7Phase4LockMonthContract()
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

        var post = root.GetProperty("paths")
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
    }
}
