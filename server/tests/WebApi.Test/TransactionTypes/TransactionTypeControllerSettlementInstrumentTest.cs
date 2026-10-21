using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TransactionTypes;

/// <summary>
/// M11 Phase 4: the explicit <c>SettlementInstrument</c> on the TransactionType admin contract — create, read, the
/// omitted-keeps / explicit-replaces update rule (decision 4.2), retirement and rename keeping the classification,
/// the fresh seed, branch isolation and the system-type database guard.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionTypeControllerSettlementInstrumentTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldPersistInstrument_AndDefaultToNoneWhenOmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentCreate", Role.Manager);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);

        var classified = await _client.PostAuthAsync("/transaction-type", new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("Cartão crédito loja")
            .WithSettlementRule(SettlementRule.TwoBusinessDays)
            .WithSettlementInstrument(SettlementInstrument.CreditCard)
            .Build(), token);
        var omitted = await _client.PostAuthAsync("/transaction-type", new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("PIX loja")
            .Build(), token);

        classified.StatusCode.ShouldBe(HttpStatusCode.Created);
        omitted.StatusCode.ShouldBe(HttpStatusCode.Created);
        var classifiedPayload = await classified.ReadContentAsync<ResponseTransactionTypeJson>();
        var omittedPayload = await omitted.ReadContentAsync<ResponseTransactionTypeJson>();
        classifiedPayload.SettlementInstrument.ShouldBe(SettlementInstrument.CreditCard);
        classifiedPayload.SettlementRule.ShouldBe(SettlementRule.TwoBusinessDays);
        omittedPayload.SettlementInstrument.ShouldBe(SettlementInstrument.None);
        (await factory.ReloadAsync<TransactionType>(classifiedPayload.Id))!.SettlementInstrument.ShouldBe(SettlementInstrument.CreditCard);

        var get = await _client.GetAuthAsync($"/transaction-type/{classifiedPayload.Id}", token);
        (await get.ReadContentAsync<ResponseTransactionTypeJson>()).SettlementInstrument.ShouldBe(SettlementInstrument.CreditCard);
    }

    [Fact]
    public async Task Update_ShouldKeepOmittedInstrument_ApplyExplicitValues_AndLeaveReferencedRowsUntouched()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentUpdate");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var type = await factory.SeedTransactionTypeAsync(category.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var row = await factory.SeedTransactionAsync(branch.Id, account.Id, type.Id, category.Id, Direction.Out, op.Id,
            user.Id, date: new DateTime(2026, 9, 1), value: 120m, dueDate: new DateTime(2026, 9, 10));

        // A migrated type starts unclassified; classifying it while rows reference it is the configuration path.
        (await factory.ReloadAsync<TransactionType>(type.Id))!.SettlementInstrument.ShouldBe(SettlementInstrument.None);
        var classify = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Depósito Cheque")
            .WithSettlementRule(SettlementRule.OperatorEnteredCheque)
            .WithSettlementInstrument(SettlementInstrument.Cheque)
            .Build(), token);
        classify.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await classify.ReadContentAsync<ResponseTransactionTypeJson>()).SettlementInstrument.ShouldBe(SettlementInstrument.Cheque);

        // A rename that omits the instrument keeps it.
        var rename = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Cheque pré-datado avulso")
            .WithSettlementRule(SettlementRule.OperatorEnteredCheque)
            .Build(), token);
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renamed = await rename.ReadContentAsync<ResponseTransactionTypeJson>();
        renamed.Name.ShouldBe("Cheque pré-datado avulso");
        renamed.SettlementInstrument.ShouldBe(SettlementInstrument.Cheque);

        // An explicit None clears it.
        var clear = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Cheque pré-datado avulso")
            .WithSettlementRule(SettlementRule.OperatorEnteredCheque)
            .WithSettlementInstrument(SettlementInstrument.None)
            .Build(), token);
        (await clear.ReadContentAsync<ResponseTransactionTypeJson>()).SettlementInstrument.ShouldBe(SettlementInstrument.None);

        // Report metadata only: the referenced row is byte-for-byte the same financial fact.
        var reloaded = await factory.ReloadAsync<Transaction>(row.Id);
        reloaded!.Value.ShouldBe(120m);
        reloaded.Direction.ShouldBe(Direction.Out);
        reloaded.DueDate.ShouldBe(new DateTime(2026, 9, 10));
        reloaded.PaidAt.ShouldBeNull();
        reloaded.Status.ShouldBe(TransactionStatus.Active);
        reloaded.Version.ShouldBe(row.Version);
    }

    [Fact]
    public async Task Deactivate_ShouldKeepTheRetiredTypesClassification()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentRetire");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var create = await _client.PostAuthAsync("/transaction-type", new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithSettlementInstrument(SettlementInstrument.DebitCard)
            .Build(), token);
        var created = await create.ReadContentAsync<ResponseTransactionTypeJson>();

        (await _client.DeleteAuthAsync($"/transaction-type/{created.Id}", token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var retired = await factory.ReloadAsync<TransactionType>(created.Id);
        retired!.Active.ShouldBeFalse();
        retired.SettlementInstrument.ShouldBe(SettlementInstrument.DebitCard);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenInstrumentNameIsUndeclared()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentInvalid");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var body = $$"""{"categoryId":"{{category.Id}}","name":"Boleto","settlementRule":"SameDay","settlementInstrument":"Boleto"}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/transaction-type");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ShouldReturn400WithResourceMessage_WhenInstrumentValueIsUndeclared()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentUndeclared");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var type = await factory.SeedTransactionTypeAsync(category.Id);

        var response = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithSettlementInstrument((SettlementInstrument)99)
            .Build(), token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_INSTRUMENT_INVALID);
    }

    [Fact]
    public async Task Update_ShouldReturn404_ForAnotherBranchsType_AndMemberShouldGet403()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtInstrumentIsolation");
        var (_, otherBranch, _, _) = await factory.SeedFullBranchContextAsync("TtInstrumentOther");
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("TtInstrumentMember", Role.Member);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Saídas", Direction.Out);
        var otherType = await factory.SeedTransactionTypeAsync(otherCategory.Id);
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithSettlementInstrument(SettlementInstrument.Cheque).Build();

        (await _client.PutAuthAsync($"/transaction-type/{otherType.Id}", request, token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PutAuthAsync($"/transaction-type/{otherType.Id}", request, memberToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ReloadAsync<TransactionType>(otherType.Id))!.SettlementInstrument.ShouldBe(SettlementInstrument.None);
        _ = branch;
    }

    [Fact]
    public async Task FreshBranch_ShouldSeedTheWorkingAnswerInstruments_AndKeepSystemTypesUnclassified()
    {
        var branch = await factory.SeedConfiguredBranchAsync($"TtInstrumentSeed {Guid.NewGuid():N}");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Admin);
        var token = factory.IssueBranchToken(membership);

        var list = await (await _client.GetAuthAsync("/transaction-type", token)).ReadContentAsync<ResponseListTransactionTypesJson>();

        list.Items.Where(item => item.SettlementInstrument != SettlementInstrument.None)
            .Select(item => (item.Name, item.SettlementInstrument))
            .OrderBy(pair => pair.Name)
            .ShouldBe([
                ("Cartão de Crédito", SettlementInstrument.CreditCard),
                ("Cartão de Débito", SettlementInstrument.DebitCard),
                ("Depósito Cheque", SettlementInstrument.Cheque)
            ]);
        list.Items.Where(item => item.SystemRole != null).ShouldAllBe(item => item.SettlementInstrument == SettlementInstrument.None);
    }

    [Fact]
    public async Task Database_ShouldRejectAClassifiedSystemType()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("TtInstrumentSystemGuard");
        var (stockSale, _) = await factory.SeedFiadoSystemTypesAsync(branch.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var tracked = await db.TransactionTypes.SingleAsync(type => type.Id == stockSale.Id);
        tracked.SettlementInstrument = SettlementInstrument.Cheque;

        var exception = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        exception.InnerException!.Message.ShouldContain("CK_TransactionTypes_SystemRole_NoSettlementInstrument");
    }

    [Fact]
    public async Task MonthlyFinancialRole_ShouldRequireTheMatchingDirection_AndKeepOmittedValuesOnUpdate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TtRoleDirection");
        var receita = await factory.SeedCategoryAsync(branch.Id, "Receita", Direction.In);
        var comerciais = await factory.SeedCategoryAsync(branch.Id, "Despesas Comerciais", Direction.Out);

        var wrong = await _client.PostAuthAsync("/transaction-type", new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(comerciais.Id)
            .WithMonthlyFinancialRole(MonthlyFinancialRole.BolaoServiceFee)
            .Build(), token);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await wrong.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_DIRECTION_INVALID);

        var fee = await (await _client.PostAuthAsync("/transaction-type", new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(receita.Id)
            .WithName("Tarifa do bolão")
            .WithMonthlyFinancialRole(MonthlyFinancialRole.BolaoServiceFee)
            .Build(), token)).ReadContentAsync<ResponseTransactionTypeJson>();
        fee.MonthlyFinancialRole.ShouldBe(MonthlyFinancialRole.BolaoServiceFee);

        var renamed = await (await _client.PutAuthAsync($"/transaction-type/{fee.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Taxa de serviço do bolão")
            .Build(), token)).ReadContentAsync<ResponseTransactionTypeJson>();
        renamed.MonthlyFinancialRole.ShouldBe(MonthlyFinancialRole.BolaoServiceFee);

        var wrongUpdate = await _client.PutAuthAsync($"/transaction-type/{fee.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Taxa de serviço do bolão")
            .WithMonthlyFinancialRole(MonthlyFinancialRole.CommercialLeftover)
            .Build(), token);
        wrongUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReloadAsync<TransactionType>(fee.Id))!.MonthlyFinancialRole.ShouldBe(MonthlyFinancialRole.BolaoServiceFee);
    }

    [Fact]
    public async Task FreshBranch_ShouldSeedTheWorkingAnswerReportMarkers()
    {
        var branch = await factory.SeedConfiguredBranchAsync($"TtRoleSeed {Guid.NewGuid():N}");
        var user = await factory.SeedUserAsync();
        var token = factory.IssueBranchToken(await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Manager));

        var list = await (await _client.GetAuthAsync("/transaction-type", token)).ReadContentAsync<ResponseListTransactionTypesJson>();

        list.Items.Where(item => item.MonthlyFinancialRole != MonthlyFinancialRole.None)
            .Select(item => (item.Name, item.MonthlyFinancialRole))
            .OrderBy(pair => pair.Name)
            .ShouldBe([
                ("Sobra de Bolão", MonthlyFinancialRole.CommercialLeftover),
                ("Sobra de Federal", MonthlyFinancialRole.CommercialLeftover),
                ("Tarifa Bolão", MonthlyFinancialRole.BolaoServiceFee)
            ]);
    }
}
