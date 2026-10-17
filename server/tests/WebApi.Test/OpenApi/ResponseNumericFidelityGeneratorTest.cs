using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.2 (Shared M2 H3) through the real generator and the real MVC serializer, on probe controllers
/// added to a derived test host only (today's contract has no instance of these shapes):
/// <list type="bullet">
/// <item>a DTO used as both request body and response gets a separate response representation: the input keeps
/// accepting numeric strings (proved by binding one), the output is numeric (proved by the response JSON), and
/// requiredness, formats and nullability match;</item>
/// <item>members the serializer writes as strings — nested inline and referenced — keep admitting a string, while their
/// plain siblings narrow, each checked against the actual response JSON.</item>
/// </list>
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class ResponseNumericFidelityGeneratorTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task SharedRequestResponseDto_ShouldPublishAFaithfulInputAndAFaithfulOutput()
    {
        await using var host = AuthTierProbeHost.Create(factory, [typeof(SharedNumbersProbeController)], null, shortCircuitActions: false);
        using var client = host.CreateClient();
        using var document = await LoadDocumentAsync(client);
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var operation = root.GetProperty("paths").GetProperty("/h3-probe/shared").GetProperty("post");

        Ref(operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"))
            .ShouldBe(nameof(H3SharedNumbersProbe));
        Ref(operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"))
            .ShouldBe(nameof(H3SharedNumbersProbe) + "Output");

        var input = schemas.GetProperty(nameof(H3SharedNumbersProbe));
        var output = schemas.GetProperty(nameof(H3SharedNumbersProbe) + "Output");
        Types(input, "count").ShouldBe(["integer", "string"]);
        Types(input, "amount").ShouldBe(["null", "number", "string"]);
        Types(output, "count").ShouldBe(["integer"]);
        Types(output, "amount").ShouldBe(["null", "number"]);
        output.GetProperty("properties").GetProperty("values").GetProperty("items").GetProperty("type").GetString().ShouldBe("integer");
        Ref(input.GetProperty("properties").GetProperty("child")).ShouldBe(nameof(H3SharedChildProbe));
        Ref(output.GetProperty("properties").GetProperty("child")).ShouldBe(nameof(H3SharedChildProbe) + "Output");
        Types(schemas.GetProperty(nameof(H3SharedChildProbe)), "total").ShouldBe(["integer", "string"]);
        Types(schemas.GetProperty(nameof(H3SharedChildProbe) + "Output"), "total").ShouldBe(["integer"]);
        foreach (var property in new[] { "count", "amount", "total" })
        {
            var (inputSchema, outputSchema) = property == "total"
                ? (schemas.GetProperty(nameof(H3SharedChildProbe)), schemas.GetProperty(nameof(H3SharedChildProbe) + "Output"))
                : (input, output);
            Format(outputSchema, property).ShouldBe(Format(inputSchema, property));
        }

        Required(output).ShouldBe(Required(input), ignoreOrder: true);

        // A real Communication DTO carries required metadata; the split keeps it, and every operation that only
        // responds with the DTO now points at the narrowed output too.
        var product = schemas.GetProperty(nameof(ResponseProductJson));
        var productOutput = schemas.GetProperty(nameof(ResponseProductJson) + "Output");
        Required(product).ShouldContain("displayOrder");
        Required(productOutput).ShouldBe(Required(product), ignoreOrder: true);
        Types(product, "displayOrder").ShouldBe(["integer", "string"]);
        Types(productOutput, "displayOrder").ShouldBe(["integer"]);
        Format(productOutput, "displayOrder").ShouldBe(Format(product, "displayOrder"));
        Ref(root.GetProperty("paths").GetProperty("/product/{productId}").GetProperty("get").GetProperty("responses")
                .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"))
            .ShouldBe(nameof(ResponseProductJson) + "Output");

        // The wire agrees with both halves: numeric strings bind, numbers come back.
        using var response = await client.PostAsync("/h3-probe/shared", new StringContent(
            """{"count":"7","amount":"2.5","values":["3"],"child":{"total":"9"}}""", Encoding.UTF8, "application/json"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("count").ValueKind.ShouldBe(JsonValueKind.Number);
        payload.RootElement.GetProperty("amount").ValueKind.ShouldBe(JsonValueKind.Number);
        payload.RootElement.GetProperty("values")[0].ValueKind.ShouldBe(JsonValueKind.Number);
        payload.RootElement.GetProperty("child").GetProperty("total").ValueKind.ShouldBe(JsonValueKind.Number);
    }

    [Fact]
    public async Task StringWrittenMembers_ShouldKeepAdmittingStrings_InlineAndReferenced()
    {
        await using var host = AuthTierProbeHost.Create(
            factory,
            [typeof(StringWrittenProbeController)],
            services => services.Configure<OpenApiOptions>("v1", options =>
            {
                var original = options.CreateSchemaReferenceId;
                options.CreateSchemaReferenceId = type =>
                    type.Type == typeof(H3InlineParentProbe) || type.Type == typeof(H3InlineChildProbe) ? null : original(type);
            }),
            shortCircuitActions: false);
        using var client = host.CreateClient();
        using var document = await LoadDocumentAsync(client);
        var root = document.RootElement;

        var inline = root.GetProperty("paths").GetProperty("/h3-probe/inline").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("properties").GetProperty("child");
        using var inlineWire = JsonDocument.Parse(await client.GetStringAsync("/h3-probe/inline"));
        var inlineChild = inlineWire.RootElement.GetProperty("child");
        inlineChild.GetProperty("asText").ValueKind.ShouldBe(JsonValueKind.String);
        Types(inline, "asText").ShouldBe(["integer", "string"]);
        inlineChild.GetProperty("plain").ValueKind.ShouldBe(JsonValueKind.Number);
        Types(inline, "plain").ShouldBe(["integer"]);

        var typed = root.GetProperty("components").GetProperty("schemas").GetProperty(nameof(H3TypedChildProbe));
        using var typedWire = JsonDocument.Parse(await client.GetStringAsync("/h3-probe/typed"));
        typedWire.RootElement.GetProperty("count").ValueKind.ShouldBe(JsonValueKind.String);
        Types(typed, "count").ShouldBe(["integer", "string"]);
    }

    private static async Task<JsonDocument> LoadDocumentAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static string Ref(JsonElement schema) => schema.GetProperty("$ref").GetString()!.Split('/')[^1];

    private static List<string> Types(JsonElement schema, string property)
    {
        var type = schema.GetProperty("properties").GetProperty(property).GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal).ToList()
            : [type.GetString()!];
    }

    private static string? Format(JsonElement schema, string property) =>
        schema.GetProperty("properties").GetProperty(property).TryGetProperty("format", out var format) ? format.GetString() : null;

    private static List<string> Required(JsonElement schema) =>
        schema.TryGetProperty("required", out var required) ? required.EnumerateArray().Select(name => name.GetString()!).ToList() : [];

    [ApiController]
    public sealed class SharedNumbersProbeController : ControllerBase
    {
        [HttpPost]
        [Route("/h3-probe/shared")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3SharedNumbersProbe), StatusCodes.Status200OK)]
        public ActionResult<H3SharedNumbersProbe> Post([FromBody] H3SharedNumbersProbe body) => body;

        [HttpPost]
        [Route("/h3-probe/product")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ResponseProductJson), StatusCodes.Status200OK)]
        public ActionResult<ResponseProductJson> Product([FromBody] ResponseProductJson body) => body;
    }

    [ApiController]
    public sealed class StringWrittenProbeController : ControllerBase
    {
        [HttpGet]
        [Route("/h3-probe/inline")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3InlineParentProbe), StatusCodes.Status200OK)]
        public ActionResult<H3InlineParentProbe> Inline() => new H3InlineParentProbe();

        [HttpGet]
        [Route("/h3-probe/typed")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3TypedChildProbe), StatusCodes.Status200OK)]
        public ActionResult<H3TypedChildProbe> Typed() => new H3TypedChildProbe();
    }

    public sealed class H3SharedNumbersProbe
    {
        public int Count { get; init; }
        public decimal? Amount { get; init; }
        public List<int> Values { get; init; } = [];
        public H3SharedChildProbe Child { get; init; } = new();
    }

    public sealed class H3SharedChildProbe
    {
        public long Total { get; init; }
    }

    public sealed class H3InlineParentProbe
    {
        public H3InlineChildProbe Child { get; init; } = new();
    }

    public sealed class H3InlineChildProbe
    {
        public int Plain { get; init; } = 1;

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public int AsText { get; init; } = 2;
    }

    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public sealed class H3TypedChildProbe
    {
        public int Count { get; init; } = 3;
    }
}
