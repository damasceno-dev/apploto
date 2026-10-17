using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.2 (Shared M2 H3) on the real generated document, independent of the snapshot text: every
/// number reachable from a response is <c>number</c>/<c>integer</c> only, while every number reachable from a request
/// body or a query parameter still admits the numeric string binding accepts. Both directions carry a non-vacuity
/// floor, so the test fails on a response widening back to <c>string</c> and on an accidental request narrowing alike.
/// The runtime half proves the document describes the wire: a numeric string is accepted on input and the response
/// writes a JSON number.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class ResponseNumericFidelityOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task ResponseReachableNumbers_ShouldNeverAdmitStrings()
    {
        using var document = await LoadDocumentAsync();
        var walk = Walk(document.RootElement, response: true);

        walk.Numbers.Count.ShouldBeGreaterThan(100, "the response walk must reach the contract's numeric members");
        walk.Numbers.Where(number => number.AdmitsString || number.HasPattern)
            .Select(number => number.Path)
            .ShouldBeEmpty("a response number is written as a JSON number, never a string");
        walk.Numbers.ShouldContain(number => number.Nullable, "nullable response numbers must be part of the walk");
        walk.SharedComponents.ShouldNotContain(id => walk.WidenedComponents.Contains(id));
    }

    [Fact]
    public async Task RequestReachableNumbers_ShouldStillAdmitTheNumericStringsBindingAccepts()
    {
        using var document = await LoadDocumentAsync();
        var walk = Walk(document.RootElement, response: false);

        walk.Numbers.Count.ShouldBeGreaterThanOrEqualTo(14, "the request walk must reach the body numeric members");
        walk.Numbers.Where(number => number.AdmitsString is false)
            .Select(number => number.Path)
            .ShouldBeEmpty("request numbers keep accepting numeric strings because binding does");
        walk.Numbers.ShouldContain(number => number.Nullable, "nullable request numbers must be part of the walk");

        var queryNumbers = QueryNumbers(document.RootElement);
        queryNumbers.Count.ShouldBeGreaterThanOrEqualTo(19);
        queryNumbers.Where(number => number.AdmitsString is false)
            .Select(number => number.Path)
            .ShouldBeEmpty("query numbers keep accepting numeric strings because binding does");
    }

    [Fact]
    public async Task Wire_ShouldAcceptANumericStringOnInputAndWriteAJsonNumberOnOutput()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("H3RoundTrip", Role.Manager);
        using var client = factory.CreateClient();
        var body = JsonContent.Create(new { name = $"Numeric {Guid.NewGuid():N}", displayOrder = "7" });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/product") { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var displayOrder = payload.RootElement.GetProperty("displayOrder");
        displayOrder.ValueKind.ShouldBe(JsonValueKind.Number);
        displayOrder.GetInt32().ShouldBe(7);
    }

    /// <summary>
    /// The narrowing is only true while nothing writes numbers as strings. Guard the two places that could change
    /// that: the MVC serializer options and any member-level <c>[JsonNumberHandling]</c> on a response contract.
    /// </summary>
    [Fact]
    public void ResponseSerializer_ShouldWriteNumbersAsNumbers()
    {
        var options = factory.Services.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;
        (options.NumberHandling & JsonNumberHandling.WriteAsString).ShouldBe(JsonNumberHandling.Strict);
        JsonSerializer.Serialize(new ResponseProductJson { DisplayOrder = 3 }, options).ShouldContain("\"displayOrder\":3");

        var stringWritten = typeof(ResponseErrorJson).Assembly.GetTypes()
            .SelectMany(type => type.GetProperties().Cast<MemberInfo>().Prepend(type))
            .Where(member => member.GetCustomAttribute<JsonNumberHandlingAttribute>() is { } attribute &&
                             (attribute.Handling & JsonNumberHandling.WriteAsString) != 0)
            .Select(member => $"{member.DeclaringType?.Name ?? member.Name}.{member.Name}")
            .ToList();
        stringWritten.ShouldBeEmpty(
            "a contract member written as a string must be published as such; the transformer reads this metadata");
    }

    private sealed record NumberSite(string Path, bool AdmitsString, bool HasPattern, bool Nullable);

    private sealed record DirectionWalk(
        List<NumberSite> Numbers,
        HashSet<string> SharedComponents,
        HashSet<string> WidenedComponents);

    private static DirectionWalk Walk(JsonElement root, bool response)
    {
        var components = root.GetProperty("components").GetProperty("schemas");
        var requestIds = Reachable(components, RequestRoots(root));
        var responseIds = Reachable(components, ResponseRoots(root));
        var ids = response ? responseIds : requestIds;

        var numbers = new List<NumberSite>();
        var widened = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var before = numbers.Count;
            CollectNumbers(components.GetProperty(id), $"#/components/schemas/{id}", numbers);
            if (numbers.Skip(before).Any(number => number.AdmitsString))
                widened.Add(id);
        }

        if (response)
        {
            foreach (var (path, schema) in ResponseRoots(root))
                CollectNumbers(schema, path, numbers);
        }

        var shared = requestIds.Intersect(responseIds, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        return new DirectionWalk(numbers, shared, widened);
    }

    private static List<NumberSite> QueryNumbers(JsonElement root)
    {
        var numbers = new List<NumberSite>();
        foreach (var (operation, path) in Operations(root))
        {
            if (operation.TryGetProperty("parameters", out var parameters) is false)
                continue;
            foreach (var parameter in parameters.EnumerateArray()
                         .Where(parameter => parameter.GetProperty("in").GetString() == "query"))
            {
                CollectNumbers(parameter.GetProperty("schema"), $"{path} query {parameter.GetProperty("name")}", numbers);
            }
        }

        return numbers;
    }

    private static IEnumerable<(JsonElement Operation, string Path)> Operations(JsonElement root)
    {
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
                yield return (operation.Value, $"{operation.Name.ToUpperInvariant()} {path.Name}");
        }
    }

    private static IEnumerable<(string Path, JsonElement Schema)> RequestRoots(JsonElement root)
    {
        foreach (var (operation, path) in Operations(root))
        {
            if (operation.TryGetProperty("parameters", out var parameters))
            {
                foreach (var parameter in parameters.EnumerateArray())
                    yield return ($"{path} parameter", parameter.GetProperty("schema"));
            }

            if (operation.TryGetProperty("requestBody", out var body))
            {
                foreach (var media in body.GetProperty("content").EnumerateObject())
                    yield return ($"{path} body", media.Value.GetProperty("schema"));
            }
        }
    }

    private static IEnumerable<(string Path, JsonElement Schema)> ResponseRoots(JsonElement root)
    {
        foreach (var (operation, path) in Operations(root))
        {
            foreach (var response in operation.GetProperty("responses").EnumerateObject())
            {
                if (response.Value.TryGetProperty("content", out var content) is false)
                    continue;
                foreach (var media in content.EnumerateObject())
                    yield return ($"{path} {response.Name}", media.Value.GetProperty("schema"));
            }
        }
    }

    private static HashSet<string> Reachable(
        JsonElement components,
        IEnumerable<(string Path, JsonElement Schema)> roots)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<JsonElement>(roots.Select(entry => entry.Schema));
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty("$ref", out var reference))
                    {
                        var id = reference.GetString()!.Split('/')[^1];
                        if (reached.Add(id))
                            pending.Push(components.GetProperty(id));
                        continue;
                    }

                    foreach (var property in element.EnumerateObject())
                        pending.Push(property.Value);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        pending.Push(item);
                    break;
            }
        }

        return reached;
    }

    /// <summary>Collects every inline numeric schema under <paramref name="schema"/>; references are walked separately.</summary>
    private static void CollectNumbers(JsonElement schema, string path, List<NumberSite> numbers)
    {
        if (schema.ValueKind != JsonValueKind.Object || schema.TryGetProperty("$ref", out _))
            return;

        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(value => value.GetString()!).ToList()
                : [type.GetString()!];
            if (types.Contains("number") || types.Contains("integer"))
            {
                numbers.Add(new NumberSite(
                    path,
                    types.Contains("string"),
                    schema.TryGetProperty("pattern", out _),
                    types.Contains("null")));
            }
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
                CollectNumbers(property.Value, $"{path}.{property.Name}", numbers);
        }

        foreach (var keyword in new[] { "items", "additionalProperties", "not" })
        {
            if (schema.TryGetProperty(keyword, out var child))
                CollectNumbers(child, $"{path}/{keyword}", numbers);
        }

        foreach (var keyword in new[] { "allOf", "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(keyword, out var branches))
            {
                var index = 0;
                foreach (var branch in branches.EnumerateArray())
                    CollectNumbers(branch, $"{path}/{keyword}[{index++}]", numbers);
            }
        }
    }

    private async Task<JsonDocument> LoadDocumentAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
