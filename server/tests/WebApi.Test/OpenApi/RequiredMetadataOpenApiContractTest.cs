using System.Net;
using System.Text.Json;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class RequiredMetadataOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Document_ShouldDeriveRequiredMetadataFromCSharpNullability()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");

        AssertRequiredMembers(
            schemas,
            "ResponseDashboardJson",
            ["date", "totalVariance", "meanVariance", "pendingApprovalCount", "closes", "notSubmitted"],
            []);
        AssertRequiredMembers(
            schemas,
            "ResponseDashboardCloseJson",
            ["dailyCloseId", "accountId", "accountName", "status"],
            ["recordedByUserId", "recordedByUserName", "submittedAt", "varianceValue"]);
        AssertRequiredMembers(
            schemas,
            "ResponseDailyCloseReviewJson",
            ["id", "accountName", "openedByUserName", "createdAt", "items"],
            ["recordedByUserId", "recordedByUserName", "approvedAt", "notes"]);
        AssertRequiredMembers(
            schemas,
            "ResponseDailyLedgerJson",
            ["items", "hasNext", "hasPrevious"],
            []);
        AssertRequiredMembers(
            schemas,
            "RequestCreateTransactionJson",
            ["date", "value", "transactionTypeId", "accountId"],
            ["description", "transactionTime", "clientId", "dueDate", "recordedByOperatorId", "saveAsDraft"]);
        AssertRequiredMembers(
            schemas,
            "RequestCreateCategoryJson",
            ["name"],
            ["defaultDirection"]);
        AssertRequiredMembers(
            schemas,
            "RequestUpsertTimeEntryJson",
            ["operatorId", "date", "status"],
            ["action", "segments"]);
        AssertSchemaHasNoRequiredMembers(schemas, "RequestUpdateSettingJson");
        AssertEnumSchemasHaveNoRequiredMembers(schemas);

        var paths = root.GetProperty("paths");
        AssertQueryParameterRequired(
            paths.GetProperty("/report/dashboard").GetProperty("get"),
            "Date",
            expected: true);

        var cashVariance = paths.GetProperty("/report/cash-variance").GetProperty("get");
        AssertQueryParameterRequired(cashVariance, "DateFrom", expected: true);
        AssertQueryParameterRequired(cashVariance, "DateTo", expected: true);
        AssertQueryParameterRequired(cashVariance, "Page", expected: true);
        AssertQueryParameterRequired(cashVariance, "PageSize", expected: true);
        AssertQueryParameterRequired(cashVariance, "AccountId", expected: false);

        AssertOptionalQueryParameters(paths, "/dailyclose", "get", "Mine", "Page", "PageSize");
        AssertOptionalQueryParameters(paths, "/holiday", "get", "Page", "PageSize");
        AssertOptionalQueryParameters(paths, "/report/daily-ledger", "get", "Page", "PageSize");
        AssertOptionalQueryParameters(paths, "/report/operator-summary", "get", "Mine");
        AssertOptionalQueryParameters(paths, "/report/timeentry-balance", "get", "Mine");
        AssertOptionalQueryParameters(paths, "/timeentry", "get", "InProgressFirst", "Page", "PageSize");
        AssertOptionalQueryParameters(paths, "/transaction", "get", "Mine", "Page", "PageSize");
        AssertOptionalBooleanQueryParameter(paths, "/dailyclose", "get", "Mine");
        AssertOptionalBooleanQueryParameter(paths, "/timeentry", "get", "Mine");
        AssertOptionalBooleanQueryParameter(paths, "/transaction", "get", "Mine");

        foreach (var operation in new[]
                 {
                     paths.GetProperty("/holiday/import-br/{year}/preview").GetProperty("get"),
                     paths.GetProperty("/holiday/import-br/{year}").GetProperty("post")
                 })
        {
            AssertDefaultedQueryParameter(operation, "includeOptionalFederal", "false");
            AssertDefaultedQueryParameter(
                operation,
                "source",
                $"\"{nameof(BrazilianHolidayCalendarSource.Composite)}\"",
                nameof(BrazilianHolidayCalendarSource));
        }
    }

    private static void AssertRequiredMembers(
        JsonElement schemas,
        string schemaName,
        IReadOnlyCollection<string> requiredMembers,
        IReadOnlyCollection<string> optionalMembers)
    {
        var schema = schemas.GetProperty(schemaName);
        var properties = schema.GetProperty("properties");
        var required = schema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(member => member.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];

        foreach (var member in requiredMembers)
        {
            properties.TryGetProperty(member, out _).ShouldBeTrue($"{schemaName}.{member} must use its JSON property name");
            required.ShouldContain(member, $"{schemaName}.{member} is declared non-nullable");
        }

        foreach (var member in optionalMembers)
        {
            properties.TryGetProperty(member, out _).ShouldBeTrue($"{schemaName}.{member} must exist in the schema");
            required.ShouldNotContain(member, $"{schemaName}.{member} is declared nullable");
        }

        foreach (var member in required)
        {
            properties.TryGetProperty(member, out _).ShouldBeTrue(
                $"{schemaName}.required must contain serialized JSON names, not raw CLR member names");
        }
    }

    private static void AssertQueryParameterRequired(JsonElement operation, string name, bool expected)
    {
        var parameter = GetQueryParameter(operation, name);
        var isRequired = parameter.TryGetProperty("required", out var required) && required.GetBoolean();
        isRequired.ShouldBe(expected, $"query parameter {name} nullability must control requiredness");
    }

    private static void AssertSchemaHasNoRequiredMembers(JsonElement schemas, string schemaName)
    {
        var schema = schemas.GetProperty(schemaName);
        schema.GetProperty("properties").EnumerateObject().ShouldNotBeEmpty();
        schema.TryGetProperty("required", out _).ShouldBeFalse(
            $"{schemaName} contains only nullable members and must not emit a required array");
    }

    private static void AssertEnumSchemasHaveNoRequiredMembers(JsonElement schemas)
    {
        var enumSchemas = typeof(DailyCloseStatus).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(DailyCloseStatus).Namespace)
            .Select(type => type.Name)
            .Where(name => schemas.TryGetProperty(name, out _))
            .ToList();

        enumSchemas.ShouldNotBeEmpty("the real document must expose representative non-Communication enum components");
        foreach (var schemaName in enumSchemas)
        {
            schemas.GetProperty(schemaName).TryGetProperty("required", out _).ShouldBeFalse(
                $"enum component {schemaName} is outside server.Communication and must remain untouched");
        }
    }

    private static void AssertDefaultedQueryParameter(
        JsonElement operation,
        string name,
        string expectedDefault,
        string? expectedSchemaName = null)
    {
        var parameter = GetQueryParameter(operation, name);
        var isRequired = parameter.TryGetProperty("required", out var required) && required.GetBoolean();
        isRequired.ShouldBeFalse($"query parameter {name} has a server default and must remain optional");
        var schema = parameter.GetProperty("schema");
        schema.GetProperty("default").GetRawText().ShouldBe(expectedDefault);
        if (expectedSchemaName is not null)
        {
            schema.GetProperty("$ref").GetString().ShouldBe(
                $"#/components/schemas/{expectedSchemaName}",
                $"defaulted enum query parameter {name} must preserve its reusable component reference");
        }
    }

    private static void AssertOptionalQueryParameters(
        JsonElement paths,
        string path,
        string method,
        params string[] parameterNames)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        foreach (var parameterName in parameterNames)
            AssertQueryParameterRequired(operation, parameterName, expected: false);
    }

    private static void AssertOptionalBooleanQueryParameter(
        JsonElement paths,
        string path,
        string method,
        string parameterName)
    {
        var parameter = GetQueryParameter(paths.GetProperty(path).GetProperty(method), parameterName);
        var isRequired = parameter.TryGetProperty("required", out var required) && required.GetBoolean();
        isRequired.ShouldBeFalse($"query parameter {parameterName} must remain optional on {path}");
        parameter.GetProperty("schema").GetProperty("type").GetString().ShouldBe(
            "boolean",
            $"query parameter {parameterName} must expose a Boolean schema on {path}");
    }

    private static JsonElement GetQueryParameter(JsonElement operation, string name)
    {
        var parameter = operation.GetProperty("parameters")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == name);

        parameter.GetProperty("in").GetString().ShouldBe("query");
        return parameter;
    }
}
