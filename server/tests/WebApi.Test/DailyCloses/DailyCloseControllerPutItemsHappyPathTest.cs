using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
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
        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items =
            [
                new RequestUpsertDailyCloseItemJson { ProductId = productA.Id, Value = 200m },
                new RequestUpsertDailyCloseItemJson { ProductId = productB.Id, Value = 75m }
            ]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await httpResponse.Content.ReadAsStringAsync());

        var payload = (await httpResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.UpdatedAt.ShouldNotBeNull();
        // Draft responses suppress CashVariance even though the physical row is preserved.
        payload.Items.Count.ShouldBe(2);
        payload.Items.ShouldContain(i => i.ProductId == productA.Id && i.Value == 200m);
        payload.Items.ShouldContain(i => i.ProductId == productB.Id && i.Value == 75m);
        payload.Items.ShouldNotContain(i => i.ProductId == cvProduct.Id);

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

    [Fact]
    public async Task PutItems_ShouldRoundTripNotesThroughSubmitAndReview_AndFreezeSubmittedNote()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutNotesRoundTrip",
            Role.Manager);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);
        const string notes = "  Diferença conferida; terminal PIX caiu às 17h.  ";
        var items =
            new[]
            {
                new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }
            };

        var putResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = items,
                Notes = notes
            },
            token);

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var putPayload = (await putResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        putPayload.Notes.ShouldBe(notes);
        putPayload.Version.ShouldNotBe(close.Version);

        var submitResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await submitResponse.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.Notes.ShouldBe(notes);

        var reviewResponse = await _client.GetAuthAsync($"/dailyclose/{close.Id}/review", token);
        reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var review = await reviewResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.Notes.ShouldBe(notes);

        var getResponse = await _client.GetAuthAsync($"/dailyclose/{close.Id}", token);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getPayload = await getResponse.ReadContentAsync<ResponseDailyCloseJson>();
        getPayload.Notes.ShouldBe(notes);

        var frozenNoteResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = submitted.Version,
                Items = items,
                Notes = "attempted overwrite while submitted"
            },
            token);
        frozenNoteResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var frozenNoteError = await frozenNoteResponse.ReadContentAsync<TestResponseErrorJson>();
        frozenNoteError.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        var stillSubmitted = await factory.ReloadAsync<DailyClose>(close.Id);
        stillSubmitted.ShouldNotBeNull();
        stillSubmitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        stillSubmitted.Notes.ShouldBe(notes);

        var recallResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);
        recallResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var recalled = await recallResponse.ReadContentAsync<ResponseDailyCloseJson>();
        recalled.Status.ShouldBe(DailyCloseStatus.Draft);
        recalled.Notes.ShouldBe(notes);
    }

    [Fact]
    public async Task PutItems_ShouldClearNotes_WhenDraftReceivesEmptyString()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutNotesClear",
            Role.Admin);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);
        var items =
            new[]
            {
                new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }
            };

        var firstResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = items,
                Notes = "temporary note"
            },
            token);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = (await firstResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;

        var clearResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = firstPayload.Version,
                Items = items,
                Notes = string.Empty
            },
            token);

        clearResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var clearPayload = (await clearResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        clearPayload.Notes.ShouldBeNull();
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Notes.ShouldBeNull();
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

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = (await httpResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        payload.Status.ShouldBe(DailyCloseStatus.Draft);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted!.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.UpdatedAt.ShouldNotBeNull();
    }

}
