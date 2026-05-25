using System.Net;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Authorization;

/// <summary>
/// Phase 7 catch-up coverage: every M6 branch-scoped endpoint declares HTTP 403
/// via <c>[ProducesResponseType(... Status403Forbidden)]</c> because the
/// <c>TokenAuthenticateBranchFilter</c> surfaces a
/// <c>TokenWithoutPermissionException</c> (→ 403) when the caller presents a
/// global-scope token instead of a branch-scope token. This test class proves the
/// declared 403 is reachable on each M6 read endpoint by issuing a real
/// global-scope JWT and asserting the response is 403 with the
/// <c>TOKEN_WITHOUT_PERMISSION</c> error key.
///
/// Mutating endpoints already cover 403 via their existing role-denied tests
/// (Member → 403 on Manager/Admin-protected actions), so they are intentionally
/// not duplicated here.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class M6BranchEndpointTokenScopeTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// Typed test data (xUnit's <see cref="TheoryData{T}"/>) for the nine M6
    /// branch-scoped read endpoints. Every M6 read endpoint is a GET, so the
    /// HTTP verb is implicit in the test name and not a parameter.
    /// </summary>
    public static TheoryData<string> BranchReadEndpoints => new()
    {
        "/category",
        $"/category/{Guid.NewGuid()}",
        "/transaction-type",
        $"/transaction-type/{Guid.NewGuid()}",
        "/product",
        $"/product/{Guid.NewGuid()}",
        "/setting",
        "/holiday",
        "/holiday/import-br/2026/preview"
    };

    [Theory]
    [MemberData(nameof(BranchReadEndpoints))]
    public async Task BranchScopedReadEndpoint_ShouldReturn403_WhenCallerPresentsGlobalScopeToken(string uri)
    {
        var user = await factory.SeedUserAsync();
        var globalToken = factory.IssueGlobalToken(user);

        var httpResponse = await _client.GetAuthAsync(uri, globalToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }
}
