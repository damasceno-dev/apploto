using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TransactionTypes;

/// <summary>
/// Happy-path coverage for TransactionType endpoints:
/// <c>POST /transaction-type</c>, <c>GET /transaction-type</c>,
/// <c>GET /transaction-type/{id}</c>, <c>PUT /transaction-type/{id}</c>,
/// and <c>DELETE /transaction-type/{id}</c>.
///
/// Permission model under test:
/// - Create / Update / Deactivate: Manager or Admin only.
/// - List / Get: any branch role.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionTypeControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /transaction-type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn201WithTransactionType_WhenAdminCreates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreateAdmin");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreateAdmin Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Name.ShouldBe(request.Name);
        payload.CategoryId.ShouldBe(category.Id);
        payload.CategoryName.ShouldBe(category.Name);
        payload.SettlementRule.ShouldBe(request.SettlementRule);

        var persisted = await factory.ReloadAsync<TransactionType>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.CategoryId.ShouldBe(category.Id);
        persisted.Name.ShouldBe(request.Name);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenManagerCreates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreateManager", Role.Manager);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreateManager Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder().WithCategoryId(category.Id).Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    public async Task Create_ShouldTrimName_WhenNameHasLeadingOrTrailingSpaces()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreateTrim");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtCreateTrim Cat");
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("  Trimmed TType  ")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Name.ShouldBe("Trimmed TType");

        var persisted = await factory.ReloadAsync<TransactionType>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("Trimmed TType");
    }

    [Fact]
    public async Task Create_ShouldAllowSameNameInDifferentCategories()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtCreateSameNameDiffCat");
        var catA = await factory.SeedCategoryAsync(branch.Id, "TtSameNameA Cat");
        var catB = await factory.SeedCategoryAsync(branch.Id, "TtSameNameB Cat");
        var sharedName = $"Shared TType Name {Guid.NewGuid():N}";
        await factory.SeedTransactionTypeAsync(catA.Id, sharedName);

        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(catB.Id)
            .WithName(sharedName)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.CategoryId.ShouldBe(catB.Id);
        payload.Name.ShouldBe(sharedName);
    }

    // -------------------------------------------------------------------------
    // GET /transaction-type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldReturn200WithTransactionTypes_WhenBranchHasActiveItems()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtList");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtList Cat");
        var tt1 = await factory.SeedTransactionTypeAsync(category.Id, "Alpha TType");
        var tt2 = await factory.SeedTransactionTypeAsync(category.Id, "Beta TType");

        var httpResponse = await _client.GetAuthAsync("/transaction-type", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListTransactionTypesJson>();
        payload.Items.ShouldContain(tt => tt.Id == tt1.Id);
        payload.Items.ShouldContain(tt => tt.Id == tt2.Id);
    }

    [Fact]
    public async Task List_ShouldReturn200_WhenMemberListsTransactionTypes()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtListMember", Role.Member);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtListMember Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Member Visible TType");

        var httpResponse = await _client.GetAuthAsync("/transaction-type", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListTransactionTypesJson>();
        payload.Items.ShouldContain(t => t.Id == tt.Id);
    }

    [Fact]
    public async Task List_ShouldIsolateTransactionTypesByBranch_WhenTwoBranchesExist()
    {
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("TtListIso1");
        var (_, branch2, _, token2) = await factory.SeedFullBranchContextAsync("TtListIso2");
        var cat1 = await factory.SeedCategoryAsync(branch1.Id, "TtListIso1 Cat");
        var cat2 = await factory.SeedCategoryAsync(branch2.Id, "TtListIso2 Cat");
        var tt1 = await factory.SeedTransactionTypeAsync(cat1.Id, "Branch1Only TType");
        var tt2 = await factory.SeedTransactionTypeAsync(cat2.Id, "Branch2Only TType");

        var response1 = await _client.GetAuthAsync("/transaction-type", token1);
        var response2 = await _client.GetAuthAsync("/transaction-type", token2);

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload1 = await response1.ReadContentAsync<ResponseListTransactionTypesJson>();
        var payload2 = await response2.ReadContentAsync<ResponseListTransactionTypesJson>();

        payload1.Items.ShouldContain(tt => tt.Id == tt1.Id);
        payload1.Items.ShouldNotContain(tt => tt.Id == tt2.Id);

        payload2.Items.ShouldContain(tt => tt.Id == tt2.Id);
        payload2.Items.ShouldNotContain(tt => tt.Id == tt1.Id);
    }

    // -------------------------------------------------------------------------
    // GET /transaction-type/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn200WithTransactionType_WhenAdminGetsExisting()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtGet");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtGet Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Get TType");

        var httpResponse = await _client.GetAuthAsync($"/transaction-type/{tt.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Id.ShouldBe(tt.Id);
        payload.CategoryId.ShouldBe(category.Id);
        payload.CategoryName.ShouldBe(category.Name);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberGetsTransactionType()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtGetMember", Role.Member);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtGetMember Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Member Get TType");

        var httpResponse = await _client.GetAuthAsync($"/transaction-type/{tt.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Id.ShouldBe(tt.Id);
    }

    // -------------------------------------------------------------------------
    // PUT /transaction-type/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenAdminUpdates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdate");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdate Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Old TType Name");
        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("New TType Name")
            .WithSettlementRule(SettlementRule.NextBusinessDay)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Id.ShouldBe(tt.Id);
        payload.Name.ShouldBe("New TType Name");
        payload.SettlementRule.ShouldBe(SettlementRule.NextBusinessDay);
        payload.CategoryId.ShouldBe(category.Id);

        var persisted = await factory.ReloadAsync<TransactionType>(tt.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("New TType Name");
        persisted.SettlementRule.ShouldBe(SettlementRule.NextBusinessDay);
        persisted.CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    public async Task Update_CategoryIdRemainsImmutable_WhenTransactionTypeIsUpdated()
    {
        // §3.10 / forward-only guarantee: CategoryId cannot be changed via Update.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdateCatImmutable");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdateCatImmutable Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Immutable Cat TType");
        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Immutable Cat TType Updated")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.CategoryId.ShouldBe(category.Id);

        var persisted = await factory.ReloadAsync<TransactionType>(tt.Id);
        persisted.ShouldNotBeNull();
        persisted.CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    public async Task Update_ForwardOnly_ShouldNotModifyExistingTransactionRows()
    {
        // Updating a TransactionType must NOT touch historical Transaction rows.
        var (user, branch, membership, token) = await factory.SeedFullBranchContextAsync("TtUpdateFwdOnly");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdateFwdOnly Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "FwdOnly TType");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: tt.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: op.Id,
            createdByUserId: user.Id,
            value: 50m);

        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("FwdOnly TType Renamed")
            .WithSettlementRule(SettlementRule.TwoBusinessDays)
            .Build();
        var updateResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reloadedTransaction = await factory.ReloadAsync<Transaction>(transaction.Id);
        reloadedTransaction.ShouldNotBeNull();
        reloadedTransaction.CategoryId.ShouldBe(category.Id);
        reloadedTransaction.Direction.ShouldBe(category.DefaultDirection);
        reloadedTransaction.Value.ShouldBe(50m);
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenManagerUpdates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtUpdateManager", Role.Manager);
        var category = await factory.SeedCategoryAsync(branch.Id, "TtUpdateManager Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Manager TType Before");
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName("Manager TType After").Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction-type/{tt.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Name.ShouldBe("Manager TType After");
    }

    // -------------------------------------------------------------------------
    // DELETE /transaction-type/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_ShouldReturn204AndSoftDelete_WhenAdminDeactivates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtDeactivate");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtDeactivate Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "To Deactivate TType");

        var httpResponse = await _client.DeleteAuthAsync($"/transaction-type/{tt.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<TransactionType>(tt.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ThenCreate_ShouldSucceedWithDifferentId_WhenSameName()
    {
        // The filtered unique index (Active = true) means a deactivated name in a category
        // is no longer a reservation. A new TT with the same name in the same category
        // gets a new Id.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtReCreate");
        var category = await factory.SeedCategoryAsync(branch.Id, "TtReCreate Cat");
        var tt = await factory.SeedTransactionTypeAsync(category.Id, "Recyclable TType");

        var deleteResponse = await _client.DeleteAuthAsync($"/transaction-type/{tt.Id}", token);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("Recyclable TType")
            .Build();
        var createResponse = await _client.PostAuthAsync("/transaction-type", request, token);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await createResponse.ReadContentAsync<ResponseTransactionTypeJson>();
        payload.Id.ShouldNotBe(tt.Id);
        payload.Name.ShouldBe("Recyclable TType");

        var newPersisted = await factory.ReloadAsync<TransactionType>(payload.Id);
        newPersisted.ShouldNotBeNull();
        newPersisted.Active.ShouldBeTrue();
    }
}
