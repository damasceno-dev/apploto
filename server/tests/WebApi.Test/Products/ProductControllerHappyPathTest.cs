using System.Net;
using CommonTestUtilities.Requests;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Products;

/// <summary>
/// Happy-path coverage for Product endpoints:
/// <c>POST /product</c>, <c>GET /product</c>, <c>GET /product/{id}</c>,
/// <c>PUT /product/{id}</c>, and <c>DELETE /product/{id}</c>.
///
/// Permission model:
/// - Create / Update / Deactivate: Manager or Admin only.
/// - List / Get: any branch role.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ProductControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /product
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn201WithProduct_WhenAdminCreatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdCreateAdmin");
        var request = new RequestCreateProductJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Name.ShouldBe(request.Name);
        payload.DisplayOrder.ShouldBe(request.DisplayOrder);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Product>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Name.ShouldBe(request.Name);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenManagerCreatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdCreateManager", Role.Manager);
        var request = new RequestCreateProductJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Create_ShouldTrimName_WhenNameHasLeadingOrTrailingSpaces()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdCreateTrim");
        var request = new RequestCreateProductJsonBuilder().WithName("  Trimmed Product  ").Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.Name.ShouldBe("Trimmed Product");

        var persisted = await factory.ReloadAsync<Product>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Trimmed Product");
    }

    // -------------------------------------------------------------------------
    // GET /product
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldReturn200WithSeededProducts_WhenAdminListsProducts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdList");
        var p1 = await factory.SeedProductAsync(branch.Id, "Alpha", displayOrder: 1);
        var p2 = await factory.SeedProductAsync(branch.Id, "Beta", displayOrder: 2);

        var httpResponse = await _client.GetAuthAsync("/product", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListProductsJson>();
        payload.Items.ShouldContain(p => p.Id == p1.Id && p.Name == p1.Name);
        payload.Items.ShouldContain(p => p.Id == p2.Id && p.Name == p2.Name);
    }

    [Fact]
    public async Task List_ShouldReturn200_WhenMemberListsProducts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdListMember", Role.Member);
        await factory.SeedProductAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync("/product", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ShouldReturnProductsOrderedByDisplayOrder()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdListOrder");
        var p3 = await factory.SeedProductAsync(branch.Id, "Gamma", displayOrder: 3);
        var p1 = await factory.SeedProductAsync(branch.Id, "Alpha", displayOrder: 1);
        var p2 = await factory.SeedProductAsync(branch.Id, "Beta", displayOrder: 2);

        var httpResponse = await _client.GetAuthAsync("/product", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListProductsJson>();
        var ids = payload.Items.Select(p => p.Id).ToList();
        ids.IndexOf(p1.Id).ShouldBeLessThan(ids.IndexOf(p2.Id));
        ids.IndexOf(p2.Id).ShouldBeLessThan(ids.IndexOf(p3.Id));
    }

    // -------------------------------------------------------------------------
    // GET /product/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn200_WhenProductExists()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdGet");
        var product = await factory.SeedProductAsync(branch.Id, "Mega-Sena");

        var httpResponse = await _client.GetAuthAsync($"/product/{product.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.Id.ShouldBe(product.Id);
        payload.Name.ShouldBe("Mega-Sena");
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberGetsProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdGetMember", Role.Member);
        var product = await factory.SeedProductAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/product/{product.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // PUT /product/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenAdminUpdatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdate");
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id, "Old Name", displayOrder: 1);
        var request = new RequestUpdateProductJsonBuilder().WithName("New Name").WithDisplayOrder(5).Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{product.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.Name.ShouldBe("New Name");
        payload.DisplayOrder.ShouldBe(5);

        var persisted = await factory.ReloadAsync<Product>(product.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("New Name");
        persisted.DisplayOrder.ShouldBe(5);
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenManagerUpdatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdateManager", Role.Manager);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id, "Original Name");
        var request = new RequestUpdateProductJsonBuilder().WithName("Updated Name").WithDisplayOrder(2).Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{product.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ShouldAllowDisplayOrderChangeOnSystemProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdateSysOrder");
        var systemProduct = await factory.SeedProductAsync(
            branch.Id,
            name: CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 1);
        var request = new RequestUpdateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .WithDisplayOrder(99)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{systemProduct.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseProductJson>();
        payload.DisplayOrder.ShouldBe(99);
        payload.Name.ShouldBe(CashVarianceProductResolver.CashVarianceProductName);

        var persisted = await factory.ReloadAsync<Product>(systemProduct.Id);
        persisted.ShouldNotBeNull();
        persisted.DisplayOrder.ShouldBe(99);
        persisted.Name.ShouldBe(CashVarianceProductResolver.CashVarianceProductName);
    }

    // -------------------------------------------------------------------------
    // DELETE /product/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_ShouldReturn204AndSoftDelete_WhenAdminDeactivatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdDeactivate");
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id, "Lotofácil");

        var httpResponse = await _client.DeleteAuthAsync($"/product/{product.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<Product>(product.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn204_WhenManagerDeactivatesProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdDeactivateManager", Role.Manager);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/product/{product.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
