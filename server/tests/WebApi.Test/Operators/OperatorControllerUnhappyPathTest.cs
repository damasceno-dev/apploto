using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Operators;

/// <summary>
/// Unhappy-path coverage for the Milestone 2 Operator endpoints. Drives the real HTTP
/// pipeline to verify the stable error contract across authentication (401), role-based
/// authorization (403), missing or cross-branch resources (404), and validation (400).
/// </summary>
[Collection(ServerApiCollection.Name)]
public class OperatorControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token on all operator endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateOperatorJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/operator",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/operator");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role is rejected by TokenAuthorize(Manager, Admin).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("OpCreateForbidden", Role.Member);
        var request = new RequestCreateOperatorJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task List_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("OpListForbidden", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/operator", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OpGetForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/operator/{op.Id}", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OpUpdateForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}", request, memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OpDeactivateForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    // -------------------------------------------------------------------------
    // 400 — Shape validation failures surfaced through the HTTP pipeline.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpCreateInvalid");
        var request = new RequestCreateOperatorJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpUpdateInvalid");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 404 — Unknown or cross-branch operator ids.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn404_WhenOperatorDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpGetMissing");

        var httpResponse = await _client.GetAuthAsync($"/operator/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenOperatorBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("OpGetAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("OpGetVictim");
        var victimOp = await factory.SeedOperatorAsync(victimBranch.Id, "Victim Op");

        var httpResponse = await _client.GetAuthAsync($"/operator/{victimOp.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenOperatorDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpUpdateMissing");
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenOperatorBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("OpUpdateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("OpUpdateVictim");
        var victimOp = await factory.SeedOperatorAsync(victimBranch.Id, "Victim Update Op");
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{victimOp.Id}", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenOperatorDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpDeactivateMissing");

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenOperatorBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("OpDeactivateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("OpDeactivateVictim");
        var victimOp = await factory.SeedOperatorAsync(victimBranch.Id, "Victim Deactivate Op");

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{victimOp.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        // Victim operator must remain untouched
        var persisted = await factory.ReloadAsync<server.Domain.Entities.Operator>(victimOp.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // 404 — User/membership validation when creating with a UserId.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn404_WhenLinkedUserDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpCreateUserMissing");
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.USER_NOT_FOUND);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenLinkedUserIsNotBranchMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OpCreateNotMember");
        var outsideUser = await factory.SeedUserAsync();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(outsideUser.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_USER_NOT_BRANCH_MEMBER);
    }

    // -------------------------------------------------------------------------
    // 409 — Duplicate active operator-user links are rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn409_WhenLinkedUserAlreadyHasActiveOperatorInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpCreateDuplicateUser");
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: targetUser.Id);
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenLinkedUserAlreadyHasActiveOperatorInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpUpdateDuplicateUser");
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: targetUser.Id);
        var opToUpdate = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/operator/{opToUpdate.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);

        var persisted = await factory.ReloadAsync<Operator>(opToUpdate.Id);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Create_ShouldReturnOne201AndOne409_WhenTwoRequestsRaceToLinkSameUser()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpCreateUserRace");
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstRequest = new RequestCreateOperatorJsonBuilder()
            .WithName("Race Operator A")
            .WithUserId(targetUser.Id)
            .Build();
        var secondRequest = new RequestCreateOperatorJsonBuilder()
            .WithName("Race Operator B")
            .WithUserId(targetUser.Id)
            .Build();

        var responses = await Task.WhenAll(
            firstClient.PostAuthAsync("/operator", firstRequest, token),
            secondClient.PostAuthAsync("/operator", secondRequest, token));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

        var conflictResponse = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        var payload = await conflictResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenLinkedUserBelongedToDeactivatedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OpCreateRelinkAfterDeactivate");
        var targetUser = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var inactiveOperator = await factory.SeedOperatorAsync(branch.Id, userId: targetUser.Id);

        var deactivateResponse = await _client.DeleteAuthAsync($"/operator/{inactiveOperator.Id}", token);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/operator", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateOperatorJson>();
        payload.UserId.ShouldBe(targetUser.Id);

        var deactivatedOperator = await factory.ReloadAsync<Operator>(inactiveOperator.Id);
        var newOperator = await factory.ReloadAsync<Operator>(payload.Id);
        deactivatedOperator.ShouldNotBeNull();
        deactivatedOperator.Active.ShouldBeFalse();
        newOperator.ShouldNotBeNull();
        newOperator.Active.ShouldBeTrue();
        newOperator.UserId.ShouldBe(targetUser.Id);
    }
}
