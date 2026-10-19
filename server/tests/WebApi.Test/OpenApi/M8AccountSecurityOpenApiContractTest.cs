using System.Text.Json;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

[Collection(ServerApiCollection.Name)]
public sealed class M8AccountSecurityOpenApiContractTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task CallerUserId_ShouldBeRequiredUuid_AndAccountResponseShouldHaveNoSecrets()
    {
        using var document = await Load();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var login = schemas.GetProperty("ResponseUserLoginJson");
        login.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ShouldContain("userId");
        var id = login.GetProperty("properties").GetProperty("userId");
        id.GetProperty("type").GetString().ShouldBe("string");
        id.GetProperty("format").GetString().ShouldBe("uuid");
        schemas.GetProperty("ResponseUserProfileJson").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order()
            .ShouldBe(["email", "hasPassword", "isPlatformOperator", "name", "userId"]);
        schemas.GetRawText().ShouldNotContain("\"sessionId\"");
        schemas.GetRawText().ShouldNotContain("\"tokenHash\"");
    }

    [Fact]
    public async Task SafeCapabilityPreviewAndOwnStatus_ShouldKeepTheirExplicitPrivacyAndNullabilityBoundary()
    {
        using var document = await Load();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("ResponseInvitationPreviewJson").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order()
            .ShouldBe(["branchName", "expiresAt", "operatorName", "role"]);
        var own = schemas.GetProperty("ResponseOwnOperatorLinkRequestJson");
        own.TryGetProperty("required", out var required).ShouldBeFalse();
        own.GetProperty("properties").TryGetProperty("request", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Recovery_ShouldPublishEmpty202_AndLegacyBootstrapShouldBeClosed()
    {
        using var document = await Load();
        var paths = document.RootElement.GetProperty("paths");
        paths.GetProperty("/user/forgot-password").GetProperty("post").GetProperty("responses").GetProperty("202")
            .TryGetProperty("content", out _).ShouldBeFalse();
        paths.TryGetProperty("/user/register", out _).ShouldBeFalse();
        paths.TryGetProperty("/branch/create", out _).ShouldBeFalse();
        paths.GetProperty("/branch/users").TryGetProperty("post", out _).ShouldBeFalse();
        paths.GetProperty("/platform/onboarding").GetProperty("post").GetProperty("security")[0]
            .TryGetProperty("IdentityBearer", out _).ShouldBeTrue();
    }

    private async Task<JsonDocument> Load() => JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
}
