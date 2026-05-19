using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Categories;

/// <summary>
/// Happy-path coverage for Category endpoints:
/// <c>POST /category</c>, <c>GET /category</c>, <c>GET /category/{id}</c>,
/// <c>PUT /category/{id}</c>, and <c>DELETE /category/{id}</c>.
///
/// Permission model under test:
/// - Create / Update / Deactivate: Manager or Admin only.
/// - List / Get: any branch role.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class CategoryControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /category
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn201WithCategory_WhenAdminCreatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatCreateAdmin");
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Name.ShouldBe(request.Name);
        payload.DefaultDirection.ShouldBe(request.DefaultDirection);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Category>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Name.ShouldBe(request.Name);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenManagerCreatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatCreateManager", Role.Manager);
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Create_ShouldTrimName_WhenNameHasLeadingOrTrailingSpaces()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatCreateTrim");
        var request = new RequestCreateCategoryJsonBuilder().WithName("  Trimmed Name  ").Build();

        var httpResponse = await _client.PostAuthAsync("/category", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Name.ShouldBe("Trimmed Name");

        var persisted = await factory.ReloadAsync<Category>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Trimmed Name");
    }

    // -------------------------------------------------------------------------
    // GET /category
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldReturn200WithSeededCategories_WhenAdminListsCategories()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatList");
        var c1 = await factory.SeedCategoryAsync(branch.Id, "Alpha");
        var c2 = await factory.SeedCategoryAsync(branch.Id, "Beta");

        var httpResponse = await _client.GetAuthAsync("/category", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListCategoriesJson>();
        payload.Items.ShouldContain(c => c.Id == c1.Id && c.Name == c1.Name);
        payload.Items.ShouldContain(c => c.Id == c2.Id && c.Name == c2.Name);
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyList_WhenBranchHasNoCategories()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CatListEmpty");

        var httpResponse = await _client.GetAuthAsync("/category", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListCategoriesJson>();
        payload.Items.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_ShouldReturn200_WhenMemberListsCategories()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatListMember", Role.Member);
        await factory.SeedCategoryAsync(branch.Id, "Member Visible");

        var httpResponse = await _client.GetAuthAsync("/category", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListCategoriesJson>();
        payload.Items.ShouldContain(c => c.Name == "Member Visible");
    }

    // -------------------------------------------------------------------------
    // GET /category/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn200WithCategory_WhenAdminGetsExistingCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatGet");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Get Test");

        var httpResponse = await _client.GetAuthAsync($"/category/{seeded.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Id.ShouldBe(seeded.Id);
        payload.Name.ShouldBe(seeded.Name);
        payload.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberGetsCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatGetMember", Role.Member);
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Member Get");

        var httpResponse = await _client.GetAuthAsync($"/category/{seeded.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Id.ShouldBe(seeded.Id);
    }

    // -------------------------------------------------------------------------
    // PUT /category/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn200AndPersistNewName_WhenAdminUpdatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdate");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Old Name");
        var request = new RequestUpdateCategoryJsonBuilder().WithName("New Name").Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Id.ShouldBe(seeded.Id);
        payload.Name.ShouldBe("New Name");
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Category>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("New Name");
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenManagerUpdatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateManager", Role.Manager);
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Before Manager");
        var request = new RequestUpdateCategoryJsonBuilder().WithName("After Manager").Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Name.ShouldBe("After Manager");
    }

    [Fact]
    public async Task Update_ShouldPreserveDefaultDirection_WhenNameIsChanged()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatUpdateDir");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Direction Category", Direction.Out);
        var request = new RequestUpdateCategoryJsonBuilder().WithName("Direction Category Updated").Build();

        var httpResponse = await _client.PutAuthAsync($"/category/{seeded.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<Category>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.DefaultDirection.ShouldBe(Direction.Out);
    }

    // -------------------------------------------------------------------------
    // DELETE /category/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_ShouldReturn204AndSoftDelete_WhenAdminDeactivatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatDeactivateAdmin");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "To Deactivate");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{seeded.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<Category>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn204_WhenManagerDeactivatesCategory()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatDeactivateManager", Role.Manager);
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Manager Deactivate");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{seeded.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<Category>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldCascadeToTransactionTypes_WhenCategoryHasActiveChildren()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatDeactivateCascade");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Cascade Category");
        var tt = await factory.SeedTransactionTypeAsync(seeded.Id, "Child TType");

        var httpResponse = await _client.DeleteAuthAsync($"/category/{seeded.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persistedCategory = await factory.ReloadAsync<Category>(seeded.Id);
        persistedCategory.ShouldNotBeNull();
        persistedCategory.Active.ShouldBeFalse();

        var persistedTt = await factory.ReloadAsync<TransactionType>(tt.Id);
        persistedTt.ShouldNotBeNull();
        persistedTt.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ThenCreate_ShouldSucceedWithDifferentId_WhenSameName()
    {
        // The filtered unique index (Active = true) means a deactivated name is no longer
        // a reservation. A fresh category with the same name gets a new Id.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CatReCreate");
        var seeded = await factory.SeedCategoryAsync(branch.Id, "Recyclable Name");

        var deleteResponse = await _client.DeleteAuthAsync($"/category/{seeded.Id}", token);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var request = new RequestCreateCategoryJsonBuilder().WithName("Recyclable Name").Build();
        var createResponse = await _client.PostAuthAsync("/category", request, token);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await createResponse.ReadContentAsync<ResponseCategoryJson>();
        payload.Id.ShouldNotBe(seeded.Id);
        payload.Name.ShouldBe("Recyclable Name");

        var newPersisted = await factory.ReloadAsync<Category>(payload.Id);
        newPersisted.ShouldNotBeNull();
        newPersisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // GET /category — branch isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldIsolateCategoriesByBranch_WhenTwoBranchesExist()
    {
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("CatListIso1");
        var (_, branch2, _, token2) = await factory.SeedFullBranchContextAsync("CatListIso2");
        var catA = await factory.SeedCategoryAsync(branch1.Id, "Branch1Only");
        var catB = await factory.SeedCategoryAsync(branch2.Id, "Branch2Only");

        var responseA = await _client.GetAuthAsync("/category", token1);
        var responseB = await _client.GetAuthAsync("/category", token2);

        responseA.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseB.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payloadA = await responseA.ReadContentAsync<ResponseListCategoriesJson>();
        var payloadB = await responseB.ReadContentAsync<ResponseListCategoriesJson>();

        payloadA.Items.ShouldContain(c => c.Id == catA.Id);
        payloadA.Items.ShouldNotContain(c => c.Id == catB.Id);

        payloadB.Items.ShouldContain(c => c.Id == catB.Id);
        payloadB.Items.ShouldNotContain(c => c.Id == catA.Id);
    }
}
