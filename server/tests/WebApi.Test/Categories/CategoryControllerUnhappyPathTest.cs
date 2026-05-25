using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Categories;

/// <summary>
/// Unhappy-path coverage for Category endpoints. Drives the real HTTP pipeline
/// to verify the stable error contract across authentication (401), role-based
/// authorization (403), missing or cross-branch resources (404), name conflict
/// (409), and validation (400).
/// </summary>
[Collection(ServerApiCollection.Name)]
public class CategoryControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token on all category endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/category",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/category");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/category/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PutAsync($"/category/{Guid.NewGuid()}",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/category/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role is rejected by TokenAuthorize(Manager, Admin) on write endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("CatCreateForbidden", Role.Member);
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("CatUpdateForbidden", Role.Member);
        var seeded = await factory.SeedCategoryAsync(branch.Id);
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("CatDeactivateForbidden", Role.Member);
        var seeded = await factory.SeedCategoryAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/category/{seeded.Id}", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var persisted = await factory.ReloadAsync<Category>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // 400 — Shape validation failures surfaced through the HTTP pipeline.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatCreateInvalidName");
        var request = new RequestCreateCategoryJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenCategoryIdIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatDeactivate400EmptyId");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{Guid.Empty}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_ID_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateInvalidName");
        var seeded = await factory.SeedCategoryAsync(branch.Id);
        var request = new RequestUpdateCategoryJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
    }

    // -------------------------------------------------------------------------
    // 404 — Unknown or cross-branch category ids.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn404_WhenCategoryDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatGetMissing");

        var httpResponse = await _client.GetAuthAsync($"/category/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenCategoryBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CatGetAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CatGetVictim");
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim Category");

        var httpResponse = await _client.GetAuthAsync($"/category/{victimCategory.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenCategoryDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateMissing");
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenCategoryBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CatUpdateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CatUpdateVictim");
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim Update Category");
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{victimCategory.Id}", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenCategoryDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatDeactivateMissing");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenCategoryBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CatDeactivateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CatDeactivateVictim");
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim Deactivate Category");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{victimCategory.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NOT_FOUND);

        var persisted = await factory.ReloadAsync<Category>(victimCategory.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // 409 — Name uniqueness conflict (application-layer check and DB filtered index).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn409_WhenNameAlreadyExistsInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatCreateNameConflict");
        await factory.SeedCategoryAsync(branch.Id, "Conflict Name");

        var request = new RequestCreateCategoryJsonBuilder()
            .WithName("Conflict Name")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenSameNameExistsInDifferentBranch()
    {
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("CatNameBranch1");
        var (_, branch2, _, _) = await factory.SeedFullBranchContextAsync("CatNameBranch2");
        var sharedName = $"CrossBranch {Guid.NewGuid():N}";
        await factory.SeedCategoryAsync(branch2.Id, sharedName);

        var request = new RequestCreateCategoryJsonBuilder()
            .WithName(sharedName)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token1);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenNameAlreadyUsedByAnotherCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateNameConflict");
        await factory.SeedCategoryAsync(branch.Id, "Taken Name");
        var target = await factory.SeedCategoryAsync(branch.Id, "Target Category");

        var request = new RequestUpdateCategoryJsonBuilder()
            .WithName("Taken Name")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{target.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenNameIsUnchanged()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateSameName");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Same Name");
        var request = new RequestUpdateCategoryJsonBuilder()
            .WithName("Same Name")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
