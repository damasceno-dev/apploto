using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace WebApi.Test.OpenApi;

/// <summary>
/// Whole-document regression gate for Milestone 7.6. The per-feature contract tests assert named
/// expectations; this class pins the entire contract surface so a transformer regression that no
/// individual feature test happens to sample still breaks the build.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class OpenApiContractSurfaceSnapshotTest(ServerWebApplicationFactory factory)
{
    private const string SnapshotDirectoryName = "Snapshots";
    private const string SnapshotFileName = "openapi-contract-surface.txt";
    private const string ReceivedFileName = "openapi-contract-surface.received.txt";

    [Fact]
    public async Task Document_ShouldMatchTheCheckedInContractSurfaceSnapshot()
    {
        using var document = await LoadDocumentAsync();

        var actual = Normalize(OpenApiContractSurface.Render(document.RootElement));
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "OpenApi", SnapshotDirectoryName, SnapshotFileName);
        var expected = File.Exists(expectedPath) ? Normalize(await File.ReadAllTextAsync(expectedPath)) : null;

        if (string.Equals(expected, actual, StringComparison.Ordinal))
            return;

        var receivedPath = WriteReceived(actual);
        expected.ShouldNotBeNull(
            $"the contract surface snapshot is missing; seed {SnapshotFileName} from {receivedPath}");
        actual.ShouldBe(
            expected,
            $"the generated OpenAPI contract surface changed. Review the diff against {receivedPath} " +
            $"and, when the change is intended, replace {SnapshotFileName} with it.");
    }

    /// <summary>
    /// Independent of the snapshot text so regenerating the snapshot can never quietly accept a
    /// numeric enum. Covers the whole document, including framework and problem schemas.
    /// </summary>
    [Fact]
    public async Task Document_ShouldNeverEmitBareNumericEnumValues()
    {
        using var document = await LoadDocumentAsync();

        var offenders = new List<string>();
        CollectNonStringEnums(document.RootElement, "$", offenders);

        offenders.ShouldBeEmpty("every enum in the document must serialize as declared names, never as numbers");
    }

    /// <summary>
    /// Every enum a contract carries must exist as a named string component whose values are exactly
    /// the CLR declared names, and no contract enum may fall back to an inline or numeric schema.
    /// </summary>
    [Fact]
    public async Task Document_ShouldExposeEveryContractEnumAsANamedStringComponent()
    {
        using var document = await LoadDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var contractEnums = CommunicationContractTypes.EnumTypes();
        contractEnums.ShouldNotBeEmpty();
        foreach (var enumType in contractEnums)
        {
            schemas.TryGetProperty(enumType.Name, out var schema).ShouldBeTrue(
                $"OpenAPI must contain a component for contract enum {enumType.FullName}");
            schema.GetProperty("type").GetString().ShouldBe("string", $"{enumType.Name} must be a named string enum");
            schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ShouldBe(
                Enum.GetNames(enumType),
                $"{enumType.Name} must advertise its canonical declared names");
        }
    }

    /// <summary>
    /// The document half is only half the guarantee: the MVC serializer must actually write those
    /// names. Asserted for every declared value of every enum a response contract can carry, so a
    /// converter registration regression fails here rather than in one sampled endpoint.
    ///
    /// Declared values are the whole contract, and that is deliberate. An <em>undefined</em> value can only
    /// reach a response from direct SQL or a future legacy import — never from an API write, because every
    /// request enum property carries a FluentValidation <c>IsInEnum</c> rule that
    /// <c>RequestEnumValidationConventionTest</c> enforces by reflection. For that unreachable-by-design case
    /// the converter writes the numeric backing value instead of throwing, since throwing after MVC has begun
    /// flushing the body truncates the response;
    /// <c>DeclaredNameEnumResponseSerializationTest.UndefinedEnumValue_ShouldReturnCompleteJsonWithNumericFallback</c>
    /// pins that boundary with a 300 KB payload. Do not "fix" the divergence by widening this assertion or the
    /// enum schema to accept numbers — that would put <c>string | number</c> into every generated TypeScript
    /// union and undo the milestone. The structural fix is a database-level enum-domain constraint, owned by
    /// the legacy-import milestone that can first introduce such a value.
    /// </summary>
    [Fact]
    public void ResponseSerializer_ShouldWriteEveryContractEnumValueAsItsDeclaredName()
    {
        var options = factory.Services.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;

        var responseEnums = CommunicationContractTypes.ResponseEnumTypes();
        responseEnums.ShouldNotBeEmpty();
        foreach (var enumType in responseEnums)
        {
            foreach (var declaredName in Enum.GetNames(enumType))
            {
                var value = Enum.Parse(enumType, declaredName);
                JsonSerializer.Serialize(value, enumType, options).ShouldBe(
                    $"\"{declaredName}\"",
                    $"{enumType.Name}.{declaredName} must serialize as its declared name on responses");
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

    private static void CollectNonStringEnums(JsonElement element, string path, List<string> offenders)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("enum", out var values) && values.ValueKind == JsonValueKind.Array)
                {
                    var nonStrings = values.EnumerateArray()
                        .Where(value => value.ValueKind != JsonValueKind.String)
                        .Select(value => value.GetRawText())
                        .ToList();
                    if (nonStrings.Count > 0)
                        offenders.Add($"{path}.enum = [{string.Join(", ", nonStrings)}]");
                }

                foreach (var property in element.EnumerateObject())
                    CollectNonStringEnums(property.Value, $"{path}.{property.Name}", offenders);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    CollectNonStringEnums(item, $"{path}[{index++}]", offenders);
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                break;
        }
    }

    /// <summary>
    /// Writes the regenerated surface next to the checked-in snapshot in the source tree when that
    /// directory is reachable, so the developer can diff and move it in one step.
    /// </summary>
    private static string WriteReceived(string actual)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "OpenApi", SnapshotDirectoryName, ReceivedFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, actual);

        var sourceDirectory = Path.Combine(SourceDirectory(), SnapshotDirectoryName);
        if (Directory.Exists(sourceDirectory) is false)
            return outputPath;

        var sourcePath = Path.Combine(sourceDirectory, ReceivedFileName);
        File.WriteAllText(sourcePath, actual);
        return sourcePath;
    }

    private static string SourceDirectory([CallerFilePath] string callerFilePath = "") =>
        Path.GetDirectoryName(callerFilePath)!;

    private static string Normalize(string content) => content.Replace("\r\n", "\n");
}
