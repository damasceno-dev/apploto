using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Operators;

/// <summary>
/// Happy-path coverage for the Milestone 2 Operator endpoints:
/// <c>POST /operator</c>, <c>GET /operator</c>, <c>GET /operator/{id}</c>,
/// <c>PUT /operator/{id}</c>, and <c>DELETE /operator/{id}</c>.
/// All endpoints require a branch-scoped token with Admin or Manager role.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class OperatorControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn201WithOperator_WhenAdminCreatesOperatorWithoutUser()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpCreate");
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(null)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateOperatorJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Name.ShouldBe(request.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.UserId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Operator>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Name.ShouldBe(request.Name);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201WithLinkedUser_WhenAdminCreatesOperatorWithBranchMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpCreateUser");
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateOperatorJson>();
        payload.UserId.ShouldBe(targetUser.Id);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Operator>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBe(targetUser.Id);
    }

    [Fact]
    public async Task List_ShouldReturn200WithSeededOperators_WhenAdminListsOperators()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpList");
        var op1 = await factory.SeedOperatorAsync(branch.Id, "Alpha Operator");
        var op2 = await factory.SeedOperatorAsync(branch.Id, "Beta Operator");

        var httpResponse = await _client.GetAuthAsync("/operator", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListOperatorsJson>();
        payload.Operators.ShouldContain(o => o.Id == op1.Id && o.Name == op1.Name);
        payload.Operators.ShouldContain(o => o.Id == op2.Id && o.Name == op2.Name);
    }

    [Fact]
    public async Task Get_ShouldReturn200WithOperator_WhenAdminGetsExistingOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpGet");
        var op = await factory.SeedOperatorAsync(branch.Id, "Get Test Operator");

        var httpResponse = await _client.GetAuthAsync($"/operator/{op.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorJson>();
        payload.Id.ShouldBe(op.Id);
        payload.Name.ShouldBe(op.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistNewName_WhenAdminUpdatesOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpUpdate");
        var op = await factory.SeedOperatorAsync(branch.Id, "Old Name");
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithName("New Name")
            .WithUserId(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorJson>();
        payload.Id.ShouldBe(op.Id);
        payload.Name.ShouldBe("New Name");
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Operator>(op.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("New Name");
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn200WithLinkedUser_WhenAdminLinksOperatorToMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpUpdateUser");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorJson>();
        payload.UserId.ShouldBe(targetUser.Id);

        var persisted = await factory.ReloadAsync<Operator>(op.Id);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBe(targetUser.Id);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn200AndSoftDelete_WhenAdminDeactivatesOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpDeactivate");
        var op = await factory.SeedOperatorAsync(branch.Id, "To Be Deactivated");

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorJson>();
        payload.Id.ShouldBe(op.Id);
        payload.Name.ShouldBe(op.Name);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Operator>(op.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldCascadeDeactivateAssignedAccounts_WhenOperatorHasLinks()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpDeactivateCascade");
        var op = await factory.SeedOperatorAsync(branch.Id, "Cascade Operator");
        var primaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Primary Cascade");
        var secondaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Secondary Cascade");
        await factory.SeedOperatorAccountAsync(op.Id, primaryAccount.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(op.Id, secondaryAccount.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var persistedOperator = await factory.ReloadAsync<Operator>(op.Id);
        var links = await factory.ListOperatorAccountsByOperatorIdAsync(op.Id);
        persistedOperator.ShouldNotBeNull();
        persistedOperator.Active.ShouldBeFalse();
        links.Count.ShouldBe(2);
        links.All(link => link.Active is false).ShouldBeTrue();
        links.All(link => link.IsPrimary is false).ShouldBeTrue();
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyList_WhenNoOperatorsExistInBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpListEmpty");

        var httpResponse = await _client.GetAuthAsync("/operator", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListOperatorsJson>();
        payload.Operators.ShouldNotBeNull();
    }
}
