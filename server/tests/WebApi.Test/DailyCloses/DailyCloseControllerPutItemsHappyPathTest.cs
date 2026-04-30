using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerPutItemsHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ──────────────────────────────────────────────
    // Draft — upsert + soft-delete + CashVariance preserved
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn200AndUpsertUpdateSoftDeleteAndPreserveCashVariance_WhenDraftAndManager()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutMgrDraft", Role.Manager);

        // The CashVariance product must exist so the resolver can resolve it.
        var cvProduct = await factory.SeedProductAsync(
            branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: DailyCloseStatus.Draft);

        var productA = await factory.SeedProductAsync(branch.Id);
        var productB = await factory.SeedProductAsync(branch.Id);
        var productC = await factory.SeedProductAsync(branch.Id);

        // Pre-seed items: A (update), C (soft-delete), CashVariance (must be preserved).
        var itemA = await factory.SeedDailyCloseItemAsync(close.Id, productA.Id, value: 50m);
        var itemC = await factory.SeedDailyCloseItemAsync(close.Id, productC.Id, value: 10m);
        var cvItem = await factory.SeedDailyCloseItemAsync(close.Id, cvProduct.Id, value: 5m);

        // Payload: A (update) + B (insert). C and CashVariance are omitted.
        var request = new RequestPutDailyCloseItemsJson
        {
            Items =
            [
                new RequestUpsertDailyCloseItemJson { ProductId = productA.Id, Value = 200m },
                new RequestUpsertDailyCloseItemJson { ProductId = productB.Id, Value = 75m }
            ]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.UpdatedAt.ShouldNotBeNull();
        // Response: A (updated) + B (inserted) + CashVariance (preserved, omitted from payload)
        payload.Items.Count.ShouldBe(3);
        payload.Items.ShouldContain(i => i.ProductId == productA.Id && i.Value == 200m);
        payload.Items.ShouldContain(i => i.ProductId == productB.Id && i.Value == 75m);
        payload.Items.ShouldContain(i => i.ProductId == cvProduct.Id && i.Value == 5m);

        // A: updated in-place
        var reloadedA = await factory.ReloadAsync<DailyCloseItem>(itemA.Id);
        reloadedA.ShouldNotBeNull();
        reloadedA!.Value.ShouldBe(200m);
        reloadedA.Active.ShouldBeTrue();

        // C: soft-deleted (omitted from payload)
        var reloadedC = await factory.ReloadAsync<DailyCloseItem>(itemC.Id);
        reloadedC.ShouldNotBeNull();
        reloadedC!.Active.ShouldBeFalse();

        // CashVariance: omitted from payload but must never be deactivated
        var reloadedCv = await factory.ReloadAsync<DailyCloseItem>(cvItem.Id);
        reloadedCv.ShouldNotBeNull();
        reloadedCv!.Active.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────
    // Rejected → Draft auto-transition
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn200AndTransitionRejectedToDraft_WhenManager()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutMgrRejected", Role.Manager);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: DailyCloseStatus.Rejected);
        var product = await factory.SeedProductAsync(branch.Id);

        var request = new RequestPutDailyCloseItemsJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted!.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.UpdatedAt.ShouldNotBeNull();
    }

    // ──────────────────────────────────────────────
    // Submitted → Draft recall (recording-operator Member, same business day)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn200AndClearSubmittedAtAndPreserveCashVariance_WhenRecordingOperatorMemberSameDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutMemberRecall", Role.Member);

        // Seed the CashVariance product — the resolver needs exactly this name.
        var cvProduct = await factory.SeedProductAsync(
            branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);

        // Use the São Paulo local date so IsSameLocalDay always passes during the test.
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var spLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date;

        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: spLocalDate,
            status: DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddHours(-1));

        // Pre-seed a CashVariance item to verify it survives the recall.
        var cvItem = await factory.SeedDailyCloseItemAsync(close.Id, cvProduct.Id, value: 5m);

        var product = await factory.SeedProductAsync(branch.Id);

        var request = new RequestPutDailyCloseItemsJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.SubmittedAt.ShouldBeNull();

        // CashVariance item was omitted from the payload but must be preserved.
        var reloadedCv = await factory.ReloadAsync<DailyCloseItem>(cvItem.Id);
        reloadedCv.ShouldNotBeNull();
        reloadedCv!.Active.ShouldBeTrue();

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted!.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.SubmittedAt.ShouldBeNull();
        persisted.UpdatedAt.ShouldNotBeNull();
    }

    // ──────────────────────────────────────────────
    // Submitted → Draft recall (Manager — always allowed, any day)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn200AndRecall_WhenManagerAndSubmittedOnOlderDay()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutMgrRecallOld", Role.Manager);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var olderDay = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date.AddDays(-2);

        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: olderDay,
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddDays(-2));

        var product = await factory.SeedProductAsync(branch.Id);

        var request = new RequestPutDailyCloseItemsJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.SubmittedAt.ShouldBeNull();

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted!.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.SubmittedAt.ShouldBeNull();
    }
}
