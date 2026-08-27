using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities.Enums;
using server.Serialization;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Serialization;

[Collection(ServerApiCollection.Name)]
public sealed class DeclaredNameEnumResponseSerializationTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public void CaseInsensitiveInput_ShouldPreserveExactCaseVariantsAndRejectAmbiguousNonExactNames()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DeclaredNameEnumJsonConverterFactory());

        JsonSerializer.Deserialize<CaseVariantEnum>("\"Active\"", options).ShouldBe(CaseVariantEnum.Active);
        JsonSerializer.Deserialize<CaseVariantEnum>("\"active\"", options).ShouldBe(CaseVariantEnum.active);
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CaseVariantEnum>("\"ACTIVE\"", options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(300_000)]
    public async Task UndefinedEnumValue_ShouldReturnCompleteJsonWithNumericFallback(int paddingLength)
    {
        await using var customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(UndefinedEnumResponseController).Assembly)));
        using var client = customFactory.CreateClient();

        using var response = await client.GetAsync($"/__tests/undefined-enum-response?paddingLength={paddingLength}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        root.GetProperty("padding").GetString()!.Length.ShouldBe(paddingLength);
        var status = root.GetProperty("status");
        status.ValueKind.ShouldBe(JsonValueKind.Number);
        status.GetInt32().ShouldBe(42);
    }

    private enum CaseVariantEnum
    {
        Active = 1,
        active = 2
    }
}

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/__tests/undefined-enum-response")]
public sealed class UndefinedEnumResponseController : ControllerBase
{
    [HttpGet]
    public ActionResult<UndefinedEnumResponseJson> Get([FromQuery] int paddingLength) =>
        new UndefinedEnumResponseJson
        {
            Padding = new string('x', paddingLength),
            Status = (TransactionStatus)42
        };
}

public sealed class UndefinedEnumResponseJson
{
    [JsonPropertyOrder(0)]
    public required string Padding { get; init; }

    [JsonPropertyOrder(1)]
    public required TransactionStatus Status { get; init; }
}
