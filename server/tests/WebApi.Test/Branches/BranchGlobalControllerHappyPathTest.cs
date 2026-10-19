using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches;

/// <summary>
/// Identity-scoped branch list/session coverage plus the M8 public-create cutover.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class BranchGlobalControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LegacyCreate_ShouldBeUnavailable_EvenWithGlobalToken()
    {
        var user = await factory.SeedUserAsync();
        var token = factory.IssueGlobalToken(user);
        var request = new RequestCreateBranchJsonBuilder()
            .WithName($"Branch {Guid.NewGuid():N}")
            .WithCnpj("12.345.678/0001-90")
            .WithAddress("Rua A, 123")
            .WithPhone("11999990000")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/create", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMyBranches_ShouldReturnOkWithSeededMemberships()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("MyBranches");
        var token = factory.IssueGlobalToken(user);

        var httpResponse = await _client.GetAuthAsync("/branch/my-branches", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListMyBranchesJson>();
        payload.Branches.ShouldContain(summary => summary.Id == branch.Id && summary.Role == Role.Admin);
    }

    [Fact]
    public async Task CreateSession_ShouldReturnOkWithBranchScopedToken()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("Session", Role.Manager);
        var token = factory.IssueGlobalToken(user);

        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(branch.Id)
            .Build();
        var httpResponse = await _client.PostAuthAsync("/branch/session", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateBranchSessionJson>();
        payload.Token.ShouldNotBeNullOrWhiteSpace();
        payload.Token.ShouldNotBe(token);
        payload.Branch.ShouldNotBeNull();
        payload.Branch.Id.ShouldBe(branch.Id);
        payload.Branch.Role.ShouldBe(Role.Manager);
    }
}
