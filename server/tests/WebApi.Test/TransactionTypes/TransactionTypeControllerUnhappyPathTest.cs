using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TransactionTypes;

/// <summary>
/// Unhappy-path coverage for TransactionType endpoints. Drives the real HTTP pipeline
/// to verify the stable error contract across authentication (401), role-based
/// authorization (403), missing or cross-branch resources (404), name conflict
/// (409), and validation (400).
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionTypeControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token on all transaction-type endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/transaction-type",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/transaction-type");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/transaction-type/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var httpResponse = await _client.PutAsync($"/transaction-type/{Guid.NewGuid()}",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/transaction-type/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — member role on write endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreateMember403", Role.Member);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreateMember403 Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder().WithCategoryId(category.Id).Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdateMember403", Role.Member);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdateMember403 Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Member Update Forbidden TType");
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtDeactivateMember403", Role.Member);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtDeactivateMember403 Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Member Deactivate Forbidden TType");

        var httpResponse = await _client.DeleteAuthAsync($"/transaction-type/{tt.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    // -------------------------------------------------------------------------
    // 404 — not found / cross-branch isolation.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn404_WhenCategoryBelongsToDifferentBranch()
    {
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("TtCreate404Cat");
        var (_, branch2, _, _) = await factory.SeedFullBranchContextAsync("TtCreate404CatB");
        var foreignCategory = await factory.SeedCategoryAsync(branch2.Id, "TtCreate404CatB Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(foreignCategory.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token1);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenTransactionTypeNotFound()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("TtGet404");

        var httpResponse = await _client.GetAuthAsync($"/transaction-type/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenTransactionTypeBelongsToDifferentBranch()
    {
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("TtGet404Cross");
        var (_, branch2, _, _) = await factory.SeedFullBranchContextAsync("TtGet404CrossB");
        var category = await factory.SeedCategoryAsync(branch2.Id, "TtGet404CrossB Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "TtGet404Cross TType");

        var httpResponse = await _client.GetAuthAsync($"/transaction-type/{tt.Id}", token1);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenTransactionTypeNotFound()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("TtUpdate404");
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenTransactionTypeNotFound()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("TtDeactivate404");

        var httpResponse = await _client.DeleteAuthAsync($"/transaction-type/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // 409 — name conflict.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn409_WhenNameAlreadyExistsInCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreate409");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreate409 Cat");
        var existingName = $"Conflicting Name {Guid.NewGuid():N}";
        await factory.SeedTransactionTypeAsync(category.Id, existingName);

        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName(existingName)
            .Build();
        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenNameAlreadyUsedInCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdate409");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdate409 Cat");
        var existingName = $"Taken Name {Guid.NewGuid():N}";
        await factory.SeedTransactionTypeAsync(category.Id, existingName);
        var ttToUpdate = await factory.SeedTransactionTypeAsync(category.Id, "Original Name TType");
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName(existingName).Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{ttToUpdate.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);
    }

    // -------------------------------------------------------------------------
    // 400 — validation errors.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreate400Name");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreate400Name Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenCategoryIdIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("TtCreate400CatId");
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(Guid.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_CATEGORY_ID_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdate400Name");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdate400Name Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Valid TType");
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName(string.Empty).Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
    }
}
