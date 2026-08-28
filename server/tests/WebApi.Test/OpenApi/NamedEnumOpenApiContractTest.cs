using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class NamedEnumOpenApiContractTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Document_ShouldAdvertiseDeclaredNamesForEveryContractEnumIncludingNullableOnlyEnums()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");

        var contractEnumTypes = CommunicationContractTypes.EnumTypes();
        contractEnumTypes.ShouldContain(
            typeof(TimeEntryTapAction),
            $"{nameof(TimeEntryTapAction)} is the nullable-only enum regression sentinel");
        foreach (var enumType in contractEnumTypes)
            AssertDeclaredComponentNames(enumType, schemas);

        AssertDeclaredComponentNames(typeof(BrazilianHolidayCalendarSource), schemas);

        AssertReference(
            schemas.GetProperty("RequestCreateTransactionTypeJson").GetProperty("properties")
                .GetProperty("settlementRule"),
            nameof(SettlementRule));
        AssertReference(
            schemas.GetProperty("RequestUpsertTimeEntryJson").GetProperty("properties")
                .GetProperty("status"),
            nameof(TimeEntryStatus));

        var nullableAction = schemas.GetProperty("RequestUpsertTimeEntryJson")
            .GetProperty("properties")
            .GetProperty("action")
            .GetProperty("oneOf")
            .EnumerateArray()
            .ToList();
        nullableAction.Any(IsNullSchema).ShouldBeTrue("nullable request enum schema must allow null");
        nullableAction.Any(schema => IsReference(schema, nameof(TimeEntryTapAction))).ShouldBeTrue(
            $"nullable request enum schema must reference {nameof(TimeEntryTapAction)}");
        AssertReference(
            schemas.GetProperty("ResponseCreateTransactionJson").GetProperty("properties")
                .GetProperty("status"),
            nameof(TransactionStatus));
        AssertReference(
            schemas.GetProperty("ResponseCreateTransactionJson").GetProperty("properties")
                .GetProperty("direction"),
            nameof(Direction));
        AssertReference(
            schemas.GetProperty("ResponseDashboardCloseJson").GetProperty("properties")
                .GetProperty("status"),
            nameof(DailyCloseStatus));
        AssertReference(
            schemas.GetProperty("ResponseFiadoAgingItemJson").GetProperty("properties")
                .GetProperty("bucket"),
            nameof(AgingBucket));

        var nullableStatus = schemas.GetProperty("ResponseCashVarianceImpactJson")
            .GetProperty("properties")
            .GetProperty("dailyCloseStatus")
            .GetProperty("oneOf")
            .EnumerateArray()
            .ToList();
        nullableStatus.Any(IsNullSchema).ShouldBeTrue("nullable enum schema must allow null");
        nullableStatus.Any(schema => IsReference(schema, nameof(DailyCloseStatus))).ShouldBeTrue(
            $"nullable enum schema must reference {nameof(DailyCloseStatus)}");

        var transactionStatusQuery = root.GetProperty("paths")
            .GetProperty("/transaction")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "Status")
            .GetProperty("schema");
        AssertReference(transactionStatusQuery, nameof(TransactionStatus));

        foreach (var operation in new[]
                 {
                     root.GetProperty("paths").GetProperty("/holiday/import-br/{year}/preview").GetProperty("get"),
                     root.GetProperty("paths").GetProperty("/holiday/import-br/{year}").GetProperty("post")
                 })
        {
            var holidaySourceQuery = operation.GetProperty("parameters")
                .EnumerateArray()
                .Single(parameter => parameter.GetProperty("name").GetString() == "source")
                .GetProperty("schema");
            AssertReference(holidaySourceQuery, nameof(BrazilianHolidayCalendarSource));
            holidaySourceQuery.GetProperty("default").GetString().ShouldBe(
                nameof(BrazilianHolidayCalendarSource.Composite));
        }
    }

    [Fact]
    public async Task TransactionTypeRequest_ShouldAcceptNamesCaseInsensitivelyAndDefinedIntegerDuringTransition()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("NamedEnumRequest", Role.Admin);
        var category = await factory.SeedCategoryAsync(branch.Id, "Named enum request category");

        using var namedResponse = await PostTransactionTypeAsync(
            category.Id,
            $"Named settlement {Guid.NewGuid():N}",
            nameof(SettlementRule.NextBusinessDay),
            token);
        using var integerResponse = await PostTransactionTypeAsync(
            category.Id,
            $"Integer settlement {Guid.NewGuid():N}",
            (int)SettlementRule.TwoBusinessDays,
            token);
        using var caseInsensitiveResponse = await PostTransactionTypeAsync(
            category.Id,
            $"Case-insensitive settlement {Guid.NewGuid():N}",
            nameof(SettlementRule.SameDay).ToLowerInvariant(),
            token);

        namedResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await namedResponse.Content.ReadAsStringAsync());
        integerResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await integerResponse.Content.ReadAsStringAsync());
        caseInsensitiveResponse.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            await caseInsensitiveResponse.Content.ReadAsStringAsync());
        await AssertStringEnumValue(namedResponse, "settlementRule", nameof(SettlementRule.NextBusinessDay));
        await AssertStringEnumValue(integerResponse, "settlementRule", nameof(SettlementRule.TwoBusinessDays));
        await AssertStringEnumValue(caseInsensitiveResponse, "settlementRule", nameof(SettlementRule.SameDay));
    }

    [Fact]
    public async Task TransactionTypeRequest_ShouldReturnLayered400MessagesForInvalidNameAndInteger()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("InvalidNamedEnumRequest", Role.Admin);
        var category = await factory.SeedCategoryAsync(branch.Id, "Invalid named enum request category");

        using var invalidName = await PostTransactionTypeAsync(
            category.Id,
            $"Invalid name {Guid.NewGuid():N}",
            "NotASettlementRule",
            token);
        using var invalidInteger = await PostTransactionTypeAsync(
            category.Id,
            $"Invalid integer {Guid.NewGuid():N}",
            999,
            token);

        await AssertInvalidRequest(invalidName, ResourcesErrorMessages.ENUM_NAME_INVALID);
        await AssertInvalidRequest(
            invalidInteger,
            ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_RULE_INVALID);
    }

    [Fact]
    public async Task TransactionTypeRequest_ShouldUseGeneric400MessageForUnrepresentableAndNonEnumInput()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("InvalidModelStateRequest", Role.Admin);
        var category = await factory.SeedCategoryAsync(branch.Id, "Invalid model-state request category");
        var validPrefix = $$"""
            {"categoryId":"{{category.Id}}","name":"Invalid model state","requiresTabAccountAndClient":false,
            """;

        foreach (var content in new HttpContent?[]
                 {
                     new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
                     new StringContent("{", System.Text.Encoding.UTF8, "application/json"),
                     new StringContent(
                         $"{validPrefix}\"settlementRule\":{{}}}}",
                         System.Text.Encoding.UTF8,
                         "application/json"),
                     new StringContent(
                         $"{validPrefix}\"settlementRule\":{ulong.MaxValue}}}",
                         System.Text.Encoding.UTF8,
                         "application/json")
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/transaction-type")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _client.SendAsync(request);

            await AssertInvalidRequest(response, ResourcesErrorMessages.REQUEST_INVALID);
        }
    }

    [Fact]
    public async Task DailyCloseResponse_ShouldSerializeStatusAsDeclaredName()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("NamedEnumDailyClose", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(SaoPauloToday())
            .WithAccountId(account.Id)
            .Build();

        using var response = await _client.PostAuthAsync("/dailyclose", request, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await AssertStringEnumValue(response, "status", nameof(DailyCloseStatus.Draft));
    }

    private async Task<HttpResponseMessage> PostTransactionTypeAsync<TEnumValue>(
        Guid categoryId,
        string name,
        TEnumValue settlementRule,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transaction-type")
        {
            Content = JsonContent.Create(new
            {
                categoryId,
                name,
                settlementRule,
                requiresTabAccountAndClient = false
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static async Task AssertInvalidRequest(HttpResponseMessage response, string expectedMessage)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldBe([expectedMessage]);
    }

    private static async Task AssertStringEnumValue(
        HttpResponseMessage response,
        string propertyName,
        string expectedName)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var value = document.RootElement.GetProperty(propertyName);
        value.ValueKind.ShouldBe(JsonValueKind.String);
        value.GetString().ShouldBe(expectedName);
    }

    private static void AssertDeclaredComponentNames(Type enumType, JsonElement schemas)
    {
        schemas.TryGetProperty(enumType.Name, out var schema).ShouldBeTrue(
            $"OpenAPI must contain a component for contract enum {enumType.FullName}");
        AssertDeclaredNames(enumType, schema);
    }

    private static void AssertDeclaredNames(Type enumType, JsonElement schema)
    {
        schema.GetProperty("type").GetString().ShouldBe("string");
        schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ShouldBe(
            Enum.GetNames(enumType));
    }

    private static void AssertReference(JsonElement schema, string schemaName) =>
        IsReference(schema, schemaName).ShouldBeTrue($"schema must reference {schemaName}");

    private static bool IsReference(JsonElement schema, string schemaName) =>
        schema.TryGetProperty("$ref", out var reference) &&
        reference.GetString() == $"#/components/schemas/{schemaName}";

    private static bool IsNullSchema(JsonElement schema) =>
        schema.TryGetProperty("type", out var type) && type.GetString() == "null";

    private static DateTime SaoPauloToday() => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).Date;
}
