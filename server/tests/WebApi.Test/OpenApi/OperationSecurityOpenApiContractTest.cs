using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.3 (Shared M2 H4), independent of the snapshot text. The document half pins the shape:
/// no document-wide requirement, exactly the two named bearer schemes, a <c>security</c> entry on every operation,
/// and the security-critical anonymous and identity sets. The runtime half sends every published operation each kind
/// of credential — none, malformed, identity, and a branch token per role — through <see cref="AuthTierProbeHost"/>
/// and requires the authorization filters to admit exactly what that operation's published <c>security</c> says.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class OperationSecurityOpenApiContractTest(ServerWebApplicationFactory factory)
{
    private const string IdentityBearer = "IdentityBearer";
    private const string BranchBearer = "BranchBearer";

    [Fact]
    public async Task Document_ShouldPublishEachOperationsOwnSecurity_AndNoDocumentRequirement()
    {
        using var document = await LoadDocumentAsync(factory);
        var root = document.RootElement;

        root.TryGetProperty("security", out _).ShouldBeFalse("there is no document-wide token requirement");
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        schemes.EnumerateObject().Select(scheme => scheme.Name).Order(StringComparer.Ordinal)
            .ShouldBe([BranchBearer, IdentityBearer]);
        foreach (var scheme in schemes.EnumerateObject())
        {
            scheme.Value.GetProperty("type").GetString().ShouldBe("http");
            scheme.Value.GetProperty("scheme").GetString().ShouldBe("bearer");
            scheme.Value.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        }

        var operations = PublishedOperations(root);
        operations.Count.ShouldBeGreaterThan(90);
        operations.Select(operation => operation.Path).ShouldNotContain(path => path.StartsWith("/report/dashboard"));
        operations.Where(operation => operation.Tier == TokenTierName.Anonymous).Select(operation => operation.Key)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["POST /user/login", "POST /user/register", "POST /user/renew-token"]);
        operations.Where(operation => operation.Tier == TokenTierName.Identity).Select(operation => operation.Key)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["GET /branch/my-branches", "POST /branch/create", "POST /branch/session"]);
        operations.ShouldContain(operation => operation.Tier == TokenTierName.Branch && operation.Roles.Count == 0);
        operations.ShouldContain(operation => operation.Tier == TokenTierName.Branch && operation.Roles.Count > 0);
        operations.SelectMany(operation => operation.Roles).ShouldAllBe(role => Enum.IsDefined(role));
    }

    [Fact]
    public async Task EveryOperation_ShouldAdmitExactlyThePublishedTokenTier_AtRuntime()
    {
        using var document = await LoadDocumentAsync(factory);
        var operations = PublishedOperations(document.RootElement);
        var credentials = await SeedCredentialsAsync(factory);

        await using var probeHost = AuthTierProbeHost.Create(factory);
        using var client = probeHost.CreateClient();

        var mismatches = new List<string>();
        foreach (var operation in operations)
        {
            foreach (var credential in credentials)
            {
                var expected = Expected(operation, credential);
                var observed = await ObserveAsync(client, operation, credential);
                if (observed != expected)
                    mismatches.Add($"{operation.Key} with {credential.Name}: expected {expected}, observed {observed}");
            }
        }

        mismatches.ShouldBeEmpty();
    }

    internal enum TokenTierName
    {
        Anonymous,
        Identity,
        Branch
    }

    internal sealed record PublishedOperation(
        string Method,
        string Path,
        IReadOnlyList<(string Name, bool IsInteger)> PathParameters,
        bool HasBody,
        TokenTierName Tier,
        IReadOnlyList<Role> Roles)
    {
        public string Key => $"{Method} {Path}";
    }

    internal sealed record Credential(string Name, string? Token, CredentialKind Kind, Role? Role = null);

    internal enum CredentialKind
    {
        None,
        Malformed,
        Identity,
        Branch
    }

    internal static async Task<IReadOnlyList<Credential>> SeedCredentialsAsync(ServerWebApplicationFactory factory)
    {
        var credentials = new List<Credential>
        {
            new("no token", null, CredentialKind.None),
            new("malformed token", "not-a-jwt", CredentialKind.Malformed)
        };
        var identityUser = await factory.SeedUserAsync();
        credentials.Add(new Credential("identity token", factory.IssueGlobalToken(identityUser), CredentialKind.Identity));
        foreach (var role in Enum.GetValues<Role>())
        {
            var (_, _, _, token) = await factory.SeedFullBranchContextAsync($"H4Probe{role}", role);
            credentials.Add(new Credential($"{role} branch token", token, CredentialKind.Branch, role));
        }

        return credentials;
    }

    internal static string Expected(PublishedOperation operation, Credential credential)
    {
        if (operation.Tier == TokenTierName.Anonymous)
            return "reached";

        return credential.Kind switch
        {
            CredentialKind.None => $"401 {ResourcesErrorMessages.TOKEN_EMPTY}",
            CredentialKind.Malformed => $"401 {ResourcesErrorMessages.TOKEN_INVALID}",
            CredentialKind.Identity => operation.Tier == TokenTierName.Identity
                ? "reached"
                : $"403 {ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION}",
            _ => operation.Tier == TokenTierName.Branch &&
                 (operation.Roles.Count == 0 || operation.Roles.Contains(credential.Role!.Value))
                ? "reached"
                : $"403 {ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION}"
        };
    }

    internal static async Task<string> ObserveAsync(HttpClient client, PublishedOperation operation, Credential credential)
    {
        var path = operation.PathParameters.Aggregate(operation.Path, (current, parameter) => current.Replace(
            $"{{{parameter.Name}}}",
            parameter.IsInteger ? (parameter.Name == "month" ? "5" : "2025") : Guid.NewGuid().ToString()));
        using var request = new HttpRequestMessage(new HttpMethod(operation.Method), path);
        if (operation.HasBody)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        if (credential.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);

        using var response = await client.SendAsync(request);
        if (response.Headers.Contains(AuthTierProbeHost.ReachedHeader))
            return "reached";

        var body = await response.Content.ReadAsStringAsync();
        var messages = body.Length == 0
            ? []
            : JsonSerializer.Deserialize<TestResponseErrorJson>(body, JsonSerializerOptions.Web)?.ErrorMessages ?? [];
        return $"{(int)response.StatusCode} {string.Join(" | ", messages)}";
    }

    internal static IReadOnlyList<PublishedOperation> PublishedOperations(JsonElement root)
    {
        var operations = new List<PublishedOperation>();
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var key = $"{operation.Name.ToUpperInvariant()} {path.Name}";
                operation.Value.TryGetProperty("security", out var security)
                    .ShouldBeTrue($"{key} must publish its own security requirement");
                var (tier, roles) = ParseSecurity(key, security);
                var pathParameters = operation.Value.TryGetProperty("parameters", out var parameters)
                    ? parameters.EnumerateArray()
                        .Where(parameter => parameter.GetProperty("in").GetString() == "path")
                        .Select(parameter => (
                            parameter.GetProperty("name").GetString()!,
                            parameter.GetProperty("schema").GetProperty("type").GetRawText().Contains("integer")))
                        .ToList()
                    : [];
                operations.Add(new PublishedOperation(
                    operation.Name.ToUpperInvariant(),
                    path.Name,
                    pathParameters,
                    operation.Value.TryGetProperty("requestBody", out _),
                    tier,
                    roles));
            }
        }

        return operations;
    }

    private static (TokenTierName Tier, IReadOnlyList<Role> Roles) ParseSecurity(string key, JsonElement security)
    {
        var requirements = security.EnumerateArray().ToList();
        if (requirements.Count == 0)
            return (TokenTierName.Anonymous, []);

        requirements.Count.ShouldBe(1, $"{key} must publish exactly one token alternative");
        var schemes = requirements[0].EnumerateObject().ToList();
        schemes.Count.ShouldBe(1, $"{key} must name exactly one scheme");
        var roles = schemes[0].Value.EnumerateArray().Select(role => Enum.Parse<Role>(role.GetString()!)).ToList();
        return schemes[0].Name switch
        {
            IdentityBearer when roles.Count == 0 => (TokenTierName.Identity, []),
            BranchBearer => (TokenTierName.Branch, roles),
            _ => throw new ShouldAssertException($"{key} publishes an unknown requirement {requirements[0].GetRawText()}")
        };
    }

    internal static async Task<JsonDocument> LoadDocumentAsync(WebApplicationFactory<Program> host)
    {
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
