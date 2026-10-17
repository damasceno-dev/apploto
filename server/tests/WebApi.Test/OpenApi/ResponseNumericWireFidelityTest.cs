using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.2 (Shared M2 H3): the generated response schema against the real response JSON, with no
/// precedence rule restated in the test. A generic comparator walks each probe response's published schema and its
/// actual wire body side by side and requires every number's schema to admit a string exactly when the serializer
/// wrote one. The probes cover every System.Text.Json number-handling source and precedence: collection and dictionary
/// types carrying their own attribute (as the root and as members), a member's property attribute overriding them both
/// ways, a declaring type's attribute overriding a collection type's, nested collections (which start again from their
/// own type) and plain numbers. Probe controllers exist on a derived test host only.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class ResponseNumericWireFidelityTest(ServerWebApplicationFactory factory)
{
    public static TheoryData<string> ProbePaths => new()
    {
        "/h3-wire/string-list",
        "/h3-wire/string-map",
        "/h3-wire/nested-lists",
        "/h3-wire/members",
        "/h3-wire/declaring"
    };

    [Theory]
    [MemberData(nameof(ProbePaths))]
    public async Task PublishedResponseSchema_ShouldAdmitAStringExactlyWhereTheWireHasOne(string path)
    {
        await using var host = AuthTierProbeHost.Create(factory, [typeof(WireProbeController)], null, shortCircuitActions: false);
        using var client = host.CreateClient();
        using var documentResponse = await client.GetAsync("/openapi/v1.json");
        documentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await documentResponse.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync());
        using var wire = JsonDocument.Parse(await client.GetStringAsync(path));

        var schema = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var comparison = new WireComparison(document.RootElement.GetProperty("components").GetProperty("schemas"));
        comparison.Compare(schema, wire.RootElement, path);

        comparison.Numbers.ShouldBeGreaterThan(0, "the probe must exercise at least one number");
        comparison.Strings.ShouldBeGreaterThan(0, "every probe includes a string-written number");
        comparison.Failures.ShouldBeEmpty();
    }

    /// <summary>Walks a schema and a JSON value together; fails each number whose schema disagrees with its wire form.</summary>
    private sealed class WireComparison(JsonElement components)
    {
        public List<string> Failures { get; } = [];
        public int Numbers { get; private set; }
        public int Strings { get; private set; }

        public void Compare(JsonElement schema, JsonElement value, string location)
        {
            schema = Resolve(schema);
            if (value.ValueKind == JsonValueKind.Null)
                return;

            foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
            {
                if (schema.TryGetProperty(keyword, out var branches))
                {
                    var candidates = branches.EnumerateArray().Select(Resolve)
                        .Where(branch => Types(branch).Contains("null") is false || Types(branch).Count > 1)
                        .ToList();
                    candidates.Count.ShouldBe(1, $"{location}: expected one non-null {keyword} branch");
                    Compare(candidates[0], value, location);
                    return;
                }
            }

            var types = Types(schema);
            if (types.Contains("number") || types.Contains("integer"))
            {
                Numbers++;
                var wireIsString = value.ValueKind == JsonValueKind.String;
                if (wireIsString)
                    Strings++;
                if (types.Contains("string") != wireIsString)
                {
                    Failures.Add($"{location}: schema [{string.Join(",", types)}] but the wire value is {value.ValueKind} ({value.GetRawText()})");
                }

                return;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in value.EnumerateArray())
                        Compare(schema.GetProperty("items"), item, $"{location}[{index++}]");
                    break;
                case JsonValueKind.Object when schema.TryGetProperty("properties", out var properties):
                    foreach (var property in value.EnumerateObject())
                        Compare(properties.GetProperty(property.Name), property.Value, $"{location}.{property.Name}");
                    break;
                case JsonValueKind.Object:
                    foreach (var entry in value.EnumerateObject())
                        Compare(schema.GetProperty("additionalProperties"), entry.Value, $"{location}{{{entry.Name}}}");
                    break;
            }
        }

        private JsonElement Resolve(JsonElement schema)
        {
            while (schema.TryGetProperty("$ref", out var reference))
                schema = components.GetProperty(reference.GetString()!.Split('/')[^1]);
            return schema;
        }

        private static List<string> Types(JsonElement schema)
        {
            if (schema.TryGetProperty("type", out var type) is false)
                return [];
            return type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(item => item.GetString()!).ToList()
                : [type.GetString()!];
        }
    }

    [ApiController]
    public sealed class WireProbeController : ControllerBase
    {
        [HttpGet]
        [Route("/h3-wire/string-list")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3StringListProbe), StatusCodes.Status200OK)]
        public ActionResult<H3StringListProbe> StringList() => new H3StringListProbe { 1, 2 };

        [HttpGet]
        [Route("/h3-wire/string-map")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3StringMapProbe), StatusCodes.Status200OK)]
        public ActionResult<H3StringMapProbe> StringMap() => new H3StringMapProbe { ["a"] = 3 };

        [HttpGet]
        [Route("/h3-wire/nested-lists")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<H3StringListProbe>), StatusCodes.Status200OK)]
        public ActionResult<List<H3StringListProbe>> NestedLists() => new List<H3StringListProbe> { new() { 4 } };

        [HttpGet]
        [Route("/h3-wire/members")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3CollectionMembersProbe), StatusCodes.Status200OK)]
        public ActionResult<H3CollectionMembersProbe> Members() => new H3CollectionMembersProbe();

        [HttpGet]
        [Route("/h3-wire/declaring")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(H3StringDeclaringProbe), StatusCodes.Status200OK)]
        public ActionResult<H3StringDeclaringProbe> Declaring() => new H3StringDeclaringProbe();
    }

    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public sealed class H3StringListProbe : List<int>;

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public sealed class H3StrictListProbe : List<int>;

    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public sealed class H3StringMapProbe : Dictionary<string, int>;

    /// <summary>No type attribute: each member's own source decides.</summary>
    public sealed class H3CollectionMembersProbe
    {
        public int Plain { get; init; } = 5;
        public H3StringListProbe Numbers { get; init; } = [6];

        [JsonNumberHandling(JsonNumberHandling.Strict)]
        public H3StringListProbe NumbersOverriddenStrict { get; init; } = [7];

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public H3StrictListProbe StrictOverriddenToString { get; init; } = [8];

        public H3StringMapProbe Map { get; init; } = new() { ["b"] = 9 };

        [JsonNumberHandling(JsonNumberHandling.Strict)]
        public H3StringMapProbe MapOverriddenStrict { get; init; } = new() { ["c"] = 10 };

        public List<H3StringListProbe> NestedStringLists { get; init; } = [[11]];
        public List<List<int>> PlainGrid { get; init; } = [[12]];
    }

    /// <summary>A declaring-type attribute: it beats a collection type's own attribute, but not a nested collection's.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public sealed class H3StringDeclaringProbe
    {
        public int Count { get; init; } = 13;
        public H3StrictListProbe StrictList { get; init; } = [14];
        public List<int> PlainList { get; init; } = [15];
        public List<List<int>> Grid { get; init; } = [[16]];
        public List<H3StrictListProbe> StrictGrid { get; init; } = [[17]];
    }
}
