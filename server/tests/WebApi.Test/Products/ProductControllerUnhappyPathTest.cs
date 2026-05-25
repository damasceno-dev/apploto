using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Products;

/// <summary>
/// Unhappy-path coverage for Product endpoints including:
/// - 400 reserved name on Create
/// - 400 PRODUCT_SYSTEM_PROTECTED on rename of system product
/// - 400 PRODUCT_SYSTEM_PROTECTED when a normal product takes the system name
/// - 409 PRODUCT_SYSTEM_PROTECTED on deactivate of system product
/// - Name reload assertions after rejected writes
/// - Auth checks
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ProductControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Auth checks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenNoToken()
    {
        var request = new RequestCreateProductJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/product", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/product");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/product/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateProductJsonBuilder().Build();

        var httpResponse = await _client.PutAsync(
            $"/product/{Guid.NewGuid()}",
            JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/product/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenMemberTriesToCreate()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdCreateMember403", Role.Member);
        var request = new RequestCreateProductJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberTriesToUpdate()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ProdUpdateMember403");
        var product = await factory.SeedProductAsync(branch.Id);
        var memberUser = await factory.SeedUserAsync();
        var memberMembership = await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member);
        var memberToken = factory.IssueBranchToken(memberMembership);
        var request = new RequestUpdateProductJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{product.Id}", request, memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenMemberTriesToDeactivate()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ProdDeactivateMember403");
        var product = await factory.SeedProductAsync(branch.Id);
        var memberUser = await factory.SeedUserAsync();
        var memberMembership = await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member);
        var memberToken = factory.IssueBranchToken(memberMembership);

        var httpResponse = await _client.DeleteAuthAsync($"/product/{product.Id}", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn404_WhenProductDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdGet404");

        var httpResponse = await _client.GetAuthAsync($"/product/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenProductDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdate404");
        var request = new RequestUpdateProductJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenProductDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdDeactivate404");

        var httpResponse = await _client.DeleteAuthAsync($"/product/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 400 validations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdCreate400Empty");
        var request = new RequestCreateProductJsonBuilder().WithName(string.Empty).Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenDisplayOrderIsNegative()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdCreate400NegOrder");
        var request = new RequestCreateProductJsonBuilder().WithDisplayOrder(-1).Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // 409 conflict (including reserved name)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn409_WhenNameIsDiferencaCaixa()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdCreateSysName");
        var request = new RequestCreateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenNameConflict()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdCreate409Conflict");
        await factory.SeedProductAsync(branch.Id, name: "Conflicting");
        var request = new RequestCreateProductJsonBuilder().WithName("Conflicting").Build();

        var httpResponse = await _client.PostAuthAsync("/product", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // -------------------------------------------------------------------------
    // System product protection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn400_WhenRenamingSystemProduct_AndNameIsUnchangedAfterRejection()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdateSysRename");
        var systemProduct = await factory.SeedProductAsync(
            branch.Id,
            name: CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 1);
        var request = new RequestUpdateProductJsonBuilder()
            .WithName("Renamed By Admin")
            .WithDisplayOrder(1)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{systemProduct.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var persisted = await factory.ReloadAsync<Product>(systemProduct.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe(CashVarianceProductResolver.CashVarianceProductName);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNormalProductTakesReservedName_AndNameIsUnchangedAfterRejection()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdateSysNameSteal");
        await factory.SeedProductAsync(
            branch.Id,
            name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(
            branch.Id,
            name: "Soda",
            displayOrder: 1);
        var request = new RequestUpdateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .WithDisplayOrder(1)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{product.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var persisted = await factory.ReloadAsync<Product>(product.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Soda");
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenRenamingToExistingProductName()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdUpdate409Conflict");
        // The update use case calls ICashVarianceProductResolver.GetIdAsync,
        // which requires the system "Diferença Caixa" product to exist.
        await factory.SeedProductAsync(
            branch.Id,
            name: CashVarianceProductResolver.CashVarianceProductName);
        await factory.SeedProductAsync(branch.Id, name: "Taken Name");
        var target = await factory.SeedProductAsync(branch.Id, name: "Target Product");
        var request = new RequestUpdateProductJsonBuilder()
            .WithName("Taken Name")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/product/{target.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);

        var persisted = await factory.ReloadAsync<Product>(target.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Target Product");
    }

    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenProductIdIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("ProdDeactivate400EmptyId");

        var httpResponse = await _client.DeleteAuthAsync($"/product/{Guid.Empty}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_ID_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn409_WhenDeactivatingSystemProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("ProdDeactivateSys");
        var systemProduct = await factory.SeedProductAsync(
            branch.Id,
            name: CashVarianceProductResolver.CashVarianceProductName);

        var httpResponse = await _client.DeleteAuthAsync($"/product/{systemProduct.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var persisted = await factory.ReloadAsync<Product>(systemProduct.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }
}
